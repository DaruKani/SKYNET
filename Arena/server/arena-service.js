'use strict';

const { Notifications } = require('./notifications');

const childProcess = require('child_process');
const crypto = require('crypto');
const path = require('path');
const {
    getPublicArenaSettings,
    validateAndMergeArenaSettings,
} = require('./arena-settings');
const {
    buildChallengeMatchups,
    buildRankingEntries,
    buildRegressionMatchups,
    buildTagEntries,
    inspectConfiguration,
    loadBotConfigText,
} = require('./bot-config');
const { normalizeRank } = require('./stats');
const { SkynetRuntime } = require('./skynet-runtime');
const { validateSkynetRuntime } = require('./skynet-config');

// M# and T# create Match and Tag rooms; keep names inside the 20-character protocol field.
const DUEL_ROOM_MIN = 100000000;
const DUEL_ROOM_MAX = 999999999;
const MAX_WINDBOT_OUTPUT_LENGTH = 2000000;
const SCORE_POLL_MS = 15000;
const SCHEDULE_POLL_MS = 1000;
const SETTLE_TIMEOUT_MS = 10 * 60 * 1000;
const WINDBOT_REQUEST_ATTEMPTS = 3;
const WINDBOT_REQUEST_TIMEOUT_MS = 3000;
const USER_STOP_REASON = 'User stopped the run from the web interface';
const TERMINAL_RUN_STATUSES = new Set(['completed', 'stopped', 'failed', 'interrupted']);
const INTERRUPTION_ERROR_CODES = new Set([
    'SRVPRO_INSTANCE_CHANGED',
    'WINDBOT_UNAVAILABLE',
]);

function requestError(message, statusCode = 400) {
    const error = new Error(message);
    error.statusCode = statusCode;
    return error;
}

function getServerInstanceId(response, body) {
    const bodyId = body?.serverInstanceId;
    const headerId = response.headers?.get?.('x-server-instance-id');
    const serverInstanceId = bodyId || headerId;
    if (typeof serverInstanceId !== 'string' || serverInstanceId === '') {
        throw new Error('SRVPro API response has no valid serverInstanceId');
    }
    if (bodyId && headerId && bodyId !== headerId) {
        throw new Error('SRVPro API serverInstanceId values do not match');
    }
    return serverInstanceId;
}

function isAbortError(error) {
    return error?.name === 'AbortError' || error?.code === 'ABORT_ERR';
}

function sleep(milliseconds, signal) {
    return new Promise((resolve, reject) => {
        if (signal?.aborted) {
            reject(signal.reason);
            return;
        }
        const onAbort = () => {
            clearTimeout(timeout);
            reject(signal.reason);
        };
        const timeout = setTimeout(() => {
            signal?.removeEventListener('abort', onAbort);
            resolve();
        }, milliseconds);
        signal?.addEventListener('abort', onAbort, { once: true });
    });
}

async function fetchWithTimeout(url, timeoutMs, signal, options) {
    const timeoutSignal = AbortSignal.timeout(timeoutMs);
    const combinedSignal = signal
        ? AbortSignal.any([signal, timeoutSignal])
        : timeoutSignal;
    return fetch(url, { ...options, signal: combinedSignal });
}

function findNetworkErrorCode(error) {
    let current = error;
    while (current) {
        if (typeof current.code === 'string') {
            return current.code;
        }
        current = current.cause;
    }
    return null;
}

async function fetchSrvproApi(url, label, timeoutMs, signal) {
    try {
        return await fetchWithTimeout(url, timeoutMs, signal);
    } catch (error) {
        if (signal?.aborted) {
            throw signal.reason || error;
        }

        const endpoint = `${url.origin}${url.pathname}`;
        if (error?.name === 'TimeoutError') {
            throw new Error(
                `${label}request timed out (${timeoutMs / 1000} seconds): ${endpoint}; confirm SRVPro is running and the management port is reachable`,
                { cause: error },
            );
        }

        const code = findNetworkErrorCode(error);
        const reason = {
            ECONNREFUSED: 'Connection refused (ECONNREFUSED); confirm SRVPro is running and its management port is configured correctly',
            ECONNRESET: 'Connection reset by peer (ECONNRESET)',
            ENETUNREACH: 'Destination network unreachable (ENETUNREACH)',
            EHOSTUNREACH: 'Destination host unreachable (EHOSTUNREACH)',
            ENOTFOUND: 'Cannot resolve destination hostname (ENOTFOUND)',
            EAI_AGAIN: 'Temporary hostname resolution failure (EAI_AGAIN)',
        }[code] || (code ? `${error.message}（${code}）` : error.message);
        throw new Error(
            `${label}request failed: ${endpoint}; ${reason || String(error)}`,
            { cause: error },
        );
    }
}

function makeHttpUrl(host, port, pathname = '/') {
    const url = new URL('http://localhost');
    url.hostname = host;
    url.port = String(port);
    url.pathname = pathname;
    return url;
}

function sanitizeEnvironment() {
    const result = {};
    const actualNames = new Map();
    for (const [name, value] of Object.entries(process.env)) {
        const normalized = name.toUpperCase();
        if (actualNames.has(normalized)) {
            delete result[actualNames.get(normalized)];
        }
        result[name] = value;
        actualNames.set(normalized, name);
    }
    return result;
}

function getRoomLaunchRate(context) {
    const configuredRate = context.settings.srvpro.roomsPerSecond ?? 1;
    return context.kind === 'tag' ? configuredRate / 2 : configuredRate;
}

class ArenaService {
    constructor(runtimeConfig, database) {
        this.runtimeConfig = runtimeConfig;
        this.database = database;
        this.notifications = new Notifications(database);
        this.pendingNotifications = new Set();
        this.contexts = new Map();
        this.localWindbots = new Map();
        this.revisions = {
            active: 0,
            runs: 0,
            system: 0,
        };
        this.windbotOutputs = {
            current: { active: false, output: '', updatedAt: null },
            old: { active: false, output: '', updatedAt: null },
        };
        this.windbotErrorLineBuffers = { current: '', old: '' };
        this.skynetRuntime = new SkynetRuntime((context, competitor, text) => {
            const name = competitor.source === 'old'
                || (competitor.source === 'target' && context.challengerVersion === 'old')
                ? 'old' : 'current';
            this.appendWindBotOutput(name, text);
        });
    }

    initialize() {
        return this.database.markActiveRunsInterrupted();
    }

    inspectSystem() {
        const { settings } = this.database.getArenaSettings();
        const configurationBySrvpro = Object.fromEntries(settings.srvpros.map((srvpro) => [
            srvpro.id,
            inspectConfiguration(settings, srvpro),
        ]));
        const inspection = configurationBySrvpro[settings.srvpros[0].id];
        return {
            botConfigFingerprints: this.getBotConfigFingerprints(settings),
            configuration: inspection,
            configurationBySrvpro,
            endpoints: {
                web: `http://${this.runtimeConfig.listenHost}:${this.runtimeConfig.listenPort}`,
            },
            srvpros: settings.srvpros.map((srvpro) => ({
                duelPort: srvpro.duelPort,
                host: srvpro.host,
                id: srvpro.id,
                name: srvpro.name,
                statusPort: srvpro.statusPort,
            })),
            windbots: Object.fromEntries(
                Object.entries(settings.windbots).map(([name, instance]) => [name, {
                    host: instance.mode !== 'remote' ? '127.0.0.1' : instance.host,
                    mode: instance.mode,
                    port: instance.mode === 'skynet' ? null : instance.port,
                }]),
            ),
        };
    }

    getBotConfigFingerprints(settings) {
        return Object.fromEntries(Object.entries(settings.windbots).map(([name, instance]) => {
            try {
                const content = loadBotConfigText(instance, name === 'current' ? 'Current' : 'Baseline');
                return [name, crypto.createHash('sha256').update(content).digest('hex')];
            } catch {
                return [name, null];
            }
        }));
    }

    getSettings() {
        const record = this.database.getArenaSettings();
        return getPublicArenaSettings(record.settings, record.updatedAt);
    }

    getRevisions() {
        return { ...this.revisions };
    }

    getSrvpro(settings, srvproId) {
        const srvpro = settings.srvpros.find((item) => item.id === srvproId);
        if (!srvpro) {
            throw requestError('SRVPro instance does not exist', 404);
        }
        return srvpro;
    }

    async listRooms(srvproId) {
        const { settings } = this.database.getArenaSettings();
        const srvpro = this.getSrvpro(settings, srvproId || settings.srvpros[0].id);
        let enableHalfwayWatch;
        let rooms;
        try {
            ({ enableHalfwayWatch, rooms } = await this.fetchRooms(srvpro));
        } catch (error) {
            throw requestError(`Cannot query SRVPro rooms: ${error.message}`, 502);
        }
        return {
            enableHalfwayWatch,
            fetchedAt: new Date().toISOString(),
            srvpro: {
                id: srvpro.id,
                name: srvpro.name,
            },
            rooms: rooms.map((room) => ({
                id: String(room.roomid ?? ''),
                mode: Number.isInteger(Number(room.roommode)) ? Number(room.roommode) : null,
                name: String(room.roomname ?? ''),
                players: Array.isArray(room.users)
                    ? room.users
                        .filter((user) => user && user.pos !== 7)
                        .sort((left, right) => left.pos - right.pos)
                        .slice(0, 4)
                        .map((user) => ({
                            name: String(user.name ?? ''),
                            position: Number(user.pos),
                            status: user.status && typeof user.status === 'object'
                                ? {
                                    lp: user.status.lp ?? null,
                                    score: user.status.score ?? null,
                                }
                                : null,
                        }))
                    : [],
                status: String(room.istart ?? ''),
            })),
        };
    }

    async updateHalfwayWatch(srvproId, enabled) {
        if (typeof enabled !== 'boolean') {
            throw requestError('Spectating permission must be a boolean');
        }
        const record = this.database.getArenaSettings();
        const srvpro = this.getSrvpro(record.settings, srvproId || record.settings.srvpros[0].id);
        try {
            await this.setSrvproHalfwayWatch(srvpro, enabled);
        } catch (error) {
            throw requestError(`Cannot set the SRVPro spectating option: ${error.message}`, 502);
        }

        return {
            enableHalfwayWatch: enabled,
            srvpro: { id: srvpro.id, name: srvpro.name },
        };
    }

    getWindBotOutput(name) {
        if (!['current', 'old'].includes(name)) {
            throw requestError('Unknown bot instance', 404);
        }
        const { settings } = this.database.getArenaSettings();
        const instance = settings.windbots[name];
        if (instance.mode === 'remote') {
            return {
                active: false,
                available: false,
                mode: 'remote',
                output: '',
                updatedAt: null,
            };
        }
        return {
            ...this.windbotOutputs[name],
            ...(instance.mode === 'skynet' ? {
                active: this.skynetRuntime.records().some((record) => {
                    const sourceName = record.competitor.source === 'old'
                        || (record.competitor.source === 'target' && record.room.context.challengerVersion === 'old')
                        ? 'old' : 'current';
                    return sourceName === name;
                }),
            } : {}),
            available: true,
            mode: instance.mode,
        };
    }

    appendWindBotOutput(name, output) {
        const entry = this.windbotOutputs[name];
        entry.output = `${entry.output}${output}`.slice(-MAX_WINDBOT_OUTPUT_LENGTH);
        entry.updatedAt = new Date().toISOString();
    }

    scanWindBotErrors(name, output, flush = false) {
        const lines = `${this.windbotErrorLineBuffers[name]}${output}`.split(/\r?\n/);
        this.windbotErrorLineBuffers[name] = flush ? '' : lines.pop();
        let recorded = false;
        for (const rawLine of lines) {
            const match = rawLine.match(/^\[\d{2}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]\s+(.+)$/);
            if (!match) {
                continue;
            }
            // Concurrent runs share local WindBot servers; stderr cannot identify the originating run.
            // Treat these shared diagnostics as smoke-check hints, not per-duel evidence.
            for (const context of this.contexts.values()) {
                if (
                    context.kind !== 'tag'
                    || !context.running
                    || name !== 'current'
                    || context.settings.windbots.current.mode !== 'local'
                ) {
                    continue;
                }
                this.database.addEvent(
                    context.id,
                    'error',
                    'windbot-output-error',
                    `WindBot output error: ${match[1]}`,
                );
                recorded = true;
            }
        }
        if (recorded) {
            this.markChanged('run-event');
        }
    }

    async updateSettings(input) {
        if (this.contexts.size > 0) {
            throw requestError('System settings cannot be changed while a run is active', 409);
        }
        const record = this.database.getArenaSettings();
        let settings;
        try {
            settings = validateAndMergeArenaSettings(input, record.settings);
        } catch (error) {
            throw requestError(error.message);
        }
        await this.fetchRemoteBotConfigs(settings);
        if (this.contexts.size > 0) {
            throw requestError('System settings cannot be changed while a run is active', 409);
        }
        const saved = this.database.saveArenaSettings(settings);
        this.markChanged('settings');
        return getPublicArenaSettings(saved.settings, saved.updatedAt);
    }

    async fetchRemoteBotConfigs(settings) {
        const sources = Object.entries(settings.windbots)
            .filter(([, instance]) => instance.mode === 'remote' && instance.botConfUrl)
            .map(async ([name, instance]) => {
                let response;
                try {
                    response = await fetchWithTimeout(instance.botConfUrl, 10000);
                } catch (error) {
                    throw requestError(
                        `${name === 'current' ? 'Current' : 'Baseline'} bot.conf URL fetch failed: ${error.message}`,
                        502,
                    );
                }
                if (!response.ok) {
                    throw requestError(
                        `${name === 'current' ? 'Current' : 'Baseline'} bot.conf URL returned HTTP ${response.status}`,
                        502,
                    );
                }
                const contentLength = Number(response.headers.get('content-length'));
                if (Number.isFinite(contentLength) && contentLength > 2 * 1024 * 1024) {
                    throw requestError(`${name === 'current' ? 'Current' : 'Baseline'} bot.conf exceeds 2 MB`, 502);
                }
                let content;
                try {
                    content = await response.text();
                } catch (error) {
                    throw requestError(
                        `${name === 'current' ? 'Current' : 'Baseline'} bot.conf URL read failed: ${error.message}`,
                        502,
                    );
                }
                if (Buffer.byteLength(content, 'utf8') > 2 * 1024 * 1024) {
                    throw requestError(`${name === 'current' ? 'Current' : 'Baseline'} bot.conf exceeds 2 MB`, 502);
                }
                if (!content.trim()) {
                    throw requestError(`${name === 'current' ? 'Current' : 'Baseline'} bot.conf URL returned empty content`, 502);
                }
                return [name, content, content !== instance.botConfText];
            });
        const fetched = await Promise.all(sources);
        for (const [name, content] of fetched) {
            settings.windbots[name].botConfText = content;
        }
        return {
            changedCount: fetched.filter(([, , changed]) => changed).length,
            fetchedCount: fetched.length,
        };
    }

    async refreshBotConfigs() {
        const record = this.database.getArenaSettings();
        const settings = structuredClone(record.settings);
        const { changedCount, fetchedCount } = await this.fetchRemoteBotConfigs(settings);
        if (fetchedCount > 0) {
            this.database.saveArenaSettings(settings);
        }
        const inspection = inspectConfiguration(settings);
        this.markChanged('decks');
        return {
            botConfigFingerprints: this.getBotConfigFingerprints(settings),
            configuration: inspection,
            contentChanged: changedCount > 0,
            fetchedRemoteCount: fetchedCount,
        };
    }

    listDecks() {
        const { settings } = this.database.getArenaSettings();
        const inspection = inspectConfiguration(settings);
        return {
            currentDecks: inspection.currentDecks,
            oldDecks: inspection.oldDecks,
            regressionDecks: inspection.decks,
        };
    }

    createRun(input = {}) {
        if (!input || typeof input !== 'object' || Array.isArray(input)) {
            throw requestError('Run settings must be an object');
        }
        const kind = input.kind || 'regression';
        if (!['challenge', 'ranking', 'regression', 'tag'].includes(kind)) {
            throw requestError('Unsupported run type');
        }
        if (input.decks !== undefined && !Array.isArray(input.decks)) {
            throw requestError('decks must be an array of deck names');
        }
        if (input.decks?.some((deck) => typeof deck !== 'string' || deck.trim() === '')) {
            throw requestError('Deck names must not be empty');
        }
        if (
            kind === 'challenge'
            && (typeof input.targetDeck !== 'string' || input.targetDeck.trim() === '')
        ) {
            throw requestError('Challenger deck name must not be empty');
        }
        const challengerVersion = kind === 'challenge'
            ? input.challengerVersion || 'current'
            : null;
        if (kind === 'challenge' && !['current', 'old'].includes(challengerVersion)) {
            throw requestError('Invalid challenger version');
        }
        let gamesPerMatchup = 0;
        if (kind === 'challenge' || kind === 'regression') {
            gamesPerMatchup = kind === 'challenge' && input.gamesPerMatchup === undefined
                ? 100
                : Number(input.gamesPerMatchup);
            if (!Number.isInteger(gamesPerMatchup) || gamesPerMatchup < 1 || gamesPerMatchup > 10000) {
                throw requestError(
                    `${kind === 'challenge' ? 'Per-opponent' : 'Per-deck'} match count must be an integer from 1 to 10000`,
                );
            }
        }

        const { settings } = this.database.getArenaSettings();
        const srvproId = typeof input.srvproId === 'string' && input.srvproId.trim()
            ? input.srvproId.trim()
            : settings.srvpros[0].id;
        const srvpro = this.getSrvpro(settings, srvproId);
        if (this.contexts.has(srvproId)) {
            throw requestError(`${srvpro.name} already has an active run`, 409);
        }
        const modeConfiguration = inspectConfiguration(settings, srvpro).modes[
            kind === 'challenge' && challengerVersion === 'old' ? 'challengeOld' : kind
        ];
        if (!modeConfiguration.valid) {
            throw requestError(`System configuration is incomplete: ${modeConfiguration.issues.join('; ')}`);
        }
        let matchups;
        let normalizedTargetDeck = null;
        try {
            if (kind === 'challenge') {
                matchups = buildChallengeMatchups(
                    settings,
                    input.targetDeck,
                    input.decks,
                    challengerVersion,
                );
                normalizedTargetDeck = matchups[0].competitors[0].deck;
            } else if (kind === 'ranking') {
                matchups = buildRankingEntries(settings, input.decks);
            } else if (kind === 'tag') {
                matchups = buildTagEntries(settings, input.decks);
            } else {
                matchups = buildRegressionMatchups(settings, input.decks);
            }
        } catch (error) {
            throw requestError(error.message);
        }
        const id = crypto.randomUUID();
        const stored = this.database.createRun({
            config: {
                ...(kind === 'challenge' ? { challengerVersion } : {}),
                duelServer: `${srvpro.host}:${srvpro.duelPort}`,
                srvproId,
                srvproName: srvpro.name,
                windbots: Object.fromEntries(
                    Object.entries(settings.windbots).map(([name, instance]) => [name, {
                        endpoint: instance.mode === 'skynet' ? null
                            : `${instance.mode === 'local' ? '127.0.0.1' : instance.host}:${instance.port}`,
                        mode: instance.mode,
                        ...(instance.mode === 'skynet' ? { skynet: structuredClone(instance.skynet) } : {}),
                    }]),
                ),
                selection: input.decks?.length ? 'selected' : 'all',
                targetDeck: normalizedTargetDeck,
            },
            createdAt: new Date().toISOString(),
            gamesPerMatchup,
            id,
            kind,
            matchups,
            srvproId,
        });
        const storedMatchupsByLabel = new Map(
            stored.matchups.map((matchup) => [matchup.label, matchup]),
        );

        const context = {
            abortController: new AbortController(),
            challengerVersion,
            finished: false,
            gamesPerMatchup,
            id,
            kind,
            latestObserved: new Map(),
            // Schedule from the creation snapshot so refreshed catalogs cannot alter active run parameters.
            matchups: matchups.map((matchup) => ({
                ...matchup,
                id: storedMatchupsByLabel.get(matchup.label).id,
                launchedGames: 0,
            })),
            nextMatchupIndex: 0,
            nextRoomNumber: crypto.randomInt(
                DUEL_ROOM_MIN,
                DUEL_ROOM_MAX + 1,
            ),
            running: false,
            settings: {
                srvpro: structuredClone(srvpro),
                windbots: structuredClone(settings.windbots),
            },
            srvproId,
            stopReason: null,
            totalGames: kind === 'ranking' || kind === 'tag'
                ? 0
                : matchups.length * gamesPerMatchup,
        };
        this.contexts.set(srvproId, context);
        this.markChanged('created');
        context.done = this.execute(context);
        return stored;
    }

    createRegressionRun(input = {}) {
        return this.createRun({ ...input, kind: 'regression' });
    }

    async execute(context) {
        const { signal } = context.abortController;
        try {
            this.database.addEvent(context.id, 'info', 'preparing', 'Restarting SRVPro');
            this.markChanged('preparing');
            await this.rebootServer(context);
            this.database.addEvent(context.id, 'info', 'server-ready', 'SRVPro restarted and is ready');
            this.markChanged('run-event');

            const instances = [['current', 'Current', context.settings.windbots.current]];
            if (
                context.kind === 'regression'
                || (context.kind === 'challenge' && context.challengerVersion === 'old')
            ) {
                instances.push(['old', 'Baseline', context.settings.windbots.old]);
            }
            const readiness = instances.map(([name, label, instance]) => {
                if (instance.mode === 'skynet') {
                    validateSkynetRuntime(instance);
                    this.database.addEvent(context.id, 'info', 'local-skynet',
                        `${label}: SKYNET one process per match participant, frozen inference, maximum ${instance.skynet.maxProcesses} processes`);
                    this.markChanged('run-event');
                    return Promise.resolve();
                }
                const endpointHost = instance.mode === 'local' ? '127.0.0.1' : instance.host;
                if (instance.mode === 'remote') {
                    this.database.addEvent(
                        context.id,
                        'info',
                        'remote-windbot',
                        `${label} uses remote WindBot ${endpointHost}:${instance.port}`,
                    );
                    this.markChanged('run-event');
                    return this.waitForWindBot(label, endpointHost, instance.port, null, signal);
                }
                return this.ensureWindBot(name, label, instance, signal);
            });
            await Promise.all(readiness);

            this.database.setRunStatus(context.id, 'running', {
                startedAt: new Date().toISOString(),
            });
            context.running = true;
            this.database.addEvent(
                context.id,
                'info',
                'running',
                `${instances.length === 2 ? 'Both' : 'Current'} AI runtimes are ready; creating rooms`,
            );
            this.markChanged('running');
            if (context.kind !== 'tag') {
                context.scorePoller = this.pollScores(context);
            }
            await this.scheduleGames(context);

            if (!context.gracefulStop && (context.kind === 'ranking' || context.kind === 'tag')) {
                throw new Error('Unbounded-run scheduling ended unexpectedly');
            }
            if (!context.gracefulStop) {
                this.database.setRunStatus(context.id, 'settling');
                this.database.addEvent(context.id, 'info', 'settling', 'All matches have been created; waiting for matches to finish');
                this.markChanged('settling');
            }
            const fullyObserved = await this.waitForResults(context);
            if (!fullyObserved) {
                this.database.addEvent(context.id, 'warning', 'settle-timeout', 'Timed out waiting for active rooms; existing results have been retained');
            }
            await this.finish(context, context.gracefulStop ? 'stopped' : 'completed',
                context.gracefulStop ? 'All active rooms finished; run complete' : 'Run complete');
        } catch (error) {
            if (isAbortError(error) && context.stopReason) {
                await this.finish(context, 'stopped', context.stopReason);
                return;
            }
            const message = error?.message || String(error);
            if (INTERRUPTION_ERROR_CODES.has(error?.code)) {
                await this.finish(context, 'interrupted', `Run interrupted: ${message}`);
                return;
            }
            await this.finish(context, 'failed', `Run failed: ${message}`, message);
        }
    }

    ensureWindBot(name, label, instance, signal) {
        let child = this.localWindbots.get(name);
        if (!child || child.exitCode !== null || child.startError || child.stopping) {
            child = this.startWindBot(name, label, instance);
        }
        return this.waitForWindBot(label, '127.0.0.1', instance.port, child, signal);
    }

    startWindBot(name, label, instance) {
        const child = childProcess.spawn(path.join(instance.runtimeDir, 'WindBot.exe'), [
            'ServerMode=True',
            `ServerPort=${instance.port}`,
            'Chat=False',
        ], {
            cwd: instance.runtimeDir,
            env: sanitizeEnvironment(),
            stdio: ['ignore', 'pipe', 'pipe'],
            windowsHide: true,
        });
        this.localWindbots.set(name, child);
        this.windbotOutputs[name] = {
            active: true,
            output: '',
            updatedAt: new Date().toISOString(),
        };
        this.windbotErrorLineBuffers[name] = '';
        // .NET Framework WindBot redirects Windows console output using the Chinese system code page.
        const captureOutput = (stream, outputPrefix, logPrefix, destination, scanErrors = false) => {
            const decoder = new TextDecoder('gbk');
            const write = (output, flush = false) => {
                if (output) {
                    this.appendWindBotOutput(name, `${outputPrefix}${output}`);
                    destination.write(`${logPrefix}${output}`);
                }
                if (scanErrors) {
                    this.scanWindBotErrors(name, output, flush);
                }
            };
            stream.on('data', (data) => write(decoder.decode(data, { stream: true })));
            stream.on('end', () => write(decoder.decode(), true));
        };
        captureOutput(child.stdout, '', `[${label}] `, process.stdout);
        captureOutput(child.stderr, '[error] ', `[${label}:error] `, process.stderr, true);
        child.on('error', (error) => {
            child.startError = error;
            this.appendWindBotOutput(name, `\n[Launch failed] ${error.message}\n`);
        });
        child.on('exit', (code, signal) => {
            if (this.localWindbots.get(name) === child) {
                this.windbotOutputs[name].active = false;
                this.localWindbots.delete(name);
                this.appendWindBotOutput(name, `\n[Process exited] code=${code}, signal=${signal || 'none'}\n`);
            }
            if (!child.stopping) {
                const error = new Error(
                    `${label} WindBot exited during an active run，code=${code}, signal=${signal || 'none'}`,
                );
                error.code = 'WINDBOT_UNAVAILABLE';
                for (const context of this.contexts.values()) {
                    const usesInstance = name === 'current'
                        || context.kind === 'regression'
                        || (context.kind === 'challenge' && context.challengerVersion === 'old');
                    if (
                        context.running
                        && usesInstance
                        && context.settings.windbots[name]?.mode === 'local'
                        && !context.abortController.signal.aborted
                    ) {
                        context.abortController.abort(error);
                    }
                }
            }
            console.log(`[${label}] WindBot exited: code=${code}, signal=${signal}`);
        });
        return child;
    }

    stopLocalWindBots() {
        for (const child of this.localWindbots.values()) {
            if (child.exitCode === null && !child.stopping) {
                child.stopping = true;
                child.kill();
            }
        }
    }

    async waitForWindBot(label, host, port, child, signal) {
        const deadline = Date.now() + 15000;
        const url = makeHttpUrl(host, port);
        while (Date.now() < deadline) {
            if (child?.startError) {
                throw child.startError;
            }
            if (child && child.exitCode !== null) {
                throw new Error(`${label} WindBot exited before its HTTP server was ready，code=${child.exitCode}`);
            }
            try {
                await fetchWithTimeout(url, 1000, signal);
                return;
            } catch (error) {
                if (signal.aborted) {
                    throw signal.reason;
                }
                await sleep(250, signal);
            }
        }
        throw new Error(`${label} WindBot HTTP server was not ready within 15 seconds: ${host}:${port}`);
    }

    async fetchRooms(srvpro, signal) {
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/getrooms');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        const response = await fetchSrvproApi(url, 'SRVPro room API ', 5000, signal);
        if (!response.ok) {
            throw new Error(`Room API returned HTTP ${response.status}`);
        }
        const body = await response.json();
        if (!Array.isArray(body.rooms)) {
            throw new Error('Room API response has no rooms array');
        }
        if (typeof body.enableHalfwayWatch !== 'boolean') {
            throw new Error('Room API response has no valid enableHalfwayWatch');
        }
        return {
            enableHalfwayWatch: body.enableHalfwayWatch,
            rooms: body.rooms,
            serverInstanceId: getServerInstanceId(response, body),
        };
    }

    async setSrvproHalfwayWatch(srvpro, enabled, signal) {
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/halfwaywatch');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        url.searchParams.set('enabled', String(enabled));
        const response = await fetchSrvproApi(url, 'SRVPro spectator settings API ', 5000, signal);
        if (!response.ok) {
            throw new Error(`Spectator settings API returned HTTP ${response.status}`);
        }
        const body = await response.json();
        if (body?.enableHalfwayWatch !== enabled) {
            throw new Error('Spectator settings API did not return the expected state');
        }
        return getServerInstanceId(response, body);
    }

    async fetchRoomCount(srvpro, signal) {
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/getroomscount');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        const response = await fetchSrvproApi(url, 'SRVPro room-count API ', 5000, signal);
        if (!response.ok) {
            throw new Error(`Room-count API returned HTTP ${response.status}`);
        }
        const body = await response.json();
        if (!Number.isSafeInteger(body?.count) || body.count < 0) {
            throw new Error('Room-count API response has no valid count');
        }
        return {
            count: body.count,
            serverInstanceId: getServerInstanceId(response, body),
        };
    }

    async queryScores(context, signal) {
        const srvpro = context.settings.srvpro;
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/getscores');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        const response = await fetchSrvproApi(url, 'SRVPro scores API ', 5000, signal);
        if (!response.ok) {
            throw new Error(`Scores API returned HTTP ${response.status}`);
        }
        const body = await response.json();
        if (!Array.isArray(body?.scores)) {
            throw new Error('Invalid scores API response');
        }
        this.assertServerInstance(context, getServerInstanceId(response, body));
        const rank = body.scores.map((score, index) => {
            if (!score || typeof score !== 'object' || Array.isArray(score)) {
                throw new Error(`Scores API scores[${index}] is not an object`);
            }
            if (typeof score.name !== 'string') {
                throw new Error(`Scores API scores[${index}].name is not a string`);
            }
            return [score.name, {
                combo: score.combo,
                flee: score.flee,
                lose: score.lose,
                win: score.win,
            }];
        });
        if (
            signal
            && (
                signal.aborted
                || context.finished
                || this.contexts.get(context.srvproId) !== context
            )
        ) {
            return;
        }
        this.receiveRank(context, rank);
    }

    recordScorePollError(context, error) {
        this.database.addEvent(
            context.id,
            'warning',
            'score-poll-error',
            `SRVPro scores query failed: ${error.message}`,
        );
        this.markChanged('score-poll-error');
    }

    async pollScores(context, intervalMs = SCORE_POLL_MS) {
        const { signal } = context.abortController;
        while (
            !signal.aborted
            && !context.finished
            && this.contexts.get(context.srvproId) === context
        ) {
            try {
                await this.queryScores(context, signal);
            } catch (error) {
                if (signal.aborted || isAbortError(error)) {
                    return;
                }
                this.recordScorePollError(context, error);
            }
            try {
                await sleep(intervalMs, signal);
            } catch (error) {
                if (signal.aborted || isAbortError(error)) {
                    return;
                }
                throw error;
            }
        }
    }

    async getRoomCount(context) {
        const { count, serverInstanceId } = await this.fetchRoomCount(
            context.settings.srvpro,
            context.abortController.signal,
        );
        this.assertServerInstance(context, serverInstanceId);
        return count;
    }

    assertServerInstance(context, serverInstanceId) {
        if (!context.serverInstanceId) {
            throw new Error('The run has not recorded an SRVPro serverInstanceId');
        }
        if (serverInstanceId === context.serverInstanceId) {
            return;
        }
        const error = new Error(
            `SRVPro instance changed (${context.serverInstanceId} -> ${serverInstanceId}); the service may have restarted during the run`,
        );
        error.code = 'SRVPRO_INSTANCE_CHANGED';
        if (!context.abortController.signal.aborted) {
            context.abortController.abort(error);
        }
        throw error;
    }

    async rebootServer(context, pollIntervalMs = 1000) {
        const srvpro = context.settings.srvpro;
        const previousServerInstanceId = (
            await this.fetchRoomCount(srvpro, context.abortController.signal)
        ).serverInstanceId;
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/message');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        url.searchParams.set('reboot', context.id);

        let response;
        let body;
        let responseRead = false;
        try {
            response = await fetchWithTimeout(url, 10000, context.abortController.signal);
            body = await response.text();
            responseRead = true;
        } catch (error) {
            if (context.abortController.signal.aborted) {
                throw context.abortController.signal.reason;
            }
            // SRVPro exits immediately after writing the reboot response; the body may be truncated.
        }
        if (responseRead) {
            if (!response.ok || body.includes('\u5bc6\u7801\u9519\u8bef') || body.includes('reboot fail')) {
                throw new Error(`Server refused to restart: HTTP ${response.status} ${body}`);
            }
            if (!body.includes('reboot ok')) {
                throw new Error(`Cannot confirm server restart: ${body}`);
            }
        }

        const deadline = Date.now() + 120000;
        let recoveredServerInstanceId = null;
        let consecutiveSuccesses = 0;
        await sleep(pollIntervalMs, context.abortController.signal);
        while (Date.now() < deadline) {
            try {
                const { serverInstanceId } = await this.fetchRoomCount(
                    srvpro,
                    context.abortController.signal,
                );
                if (serverInstanceId === previousServerInstanceId) {
                    recoveredServerInstanceId = null;
                    consecutiveSuccesses = 0;
                } else if (serverInstanceId === recoveredServerInstanceId) {
                    consecutiveSuccesses++;
                } else {
                    recoveredServerInstanceId = serverInstanceId;
                    consecutiveSuccesses = 1;
                }
                if (consecutiveSuccesses >= 2) {
                    context.serverInstanceId = recoveredServerInstanceId;
                    return;
                }
            } catch (error) {
                if (context.abortController.signal.aborted) {
                    throw context.abortController.signal.reason;
                }
                recoveredServerInstanceId = null;
                consecutiveSuccesses = 0;
            }
            await sleep(pollIntervalMs, context.abortController.signal);
        }
        throw new Error('Server did not recover within 120 seconds of restart');
    }

    nextDuelPassword(context) {
        const roomNumber = context.nextRoomNumber
            ?? crypto.randomInt(DUEL_ROOM_MIN, DUEL_ROOM_MAX + 1);
        context.nextRoomNumber = roomNumber === DUEL_ROOM_MAX
            ? DUEL_ROOM_MIN
            : roomNumber + 1;
        return `${context.kind === 'tag' ? 'T' : 'M'}#${roomNumber}`;
    }

    async addBot(context, competitor, password) {
        if (competitor.executionMode === 'skynet') {
            return this.skynetRuntime.launch(context, competitor, password);
        }
        const srvpro = context.settings.srvpro;
        const url = makeHttpUrl(competitor.endpointHost, competitor.endpointPort);
        url.searchParams.set('name', competitor.rankName);
        url.searchParams.set('host', srvpro.host);
        url.searchParams.set('port', String(srvpro.duelPort));
        url.searchParams.set('password', password);
        url.searchParams.set('deck', competitor.deck);
        url.searchParams.set('chat', 'false');
        if (competitor.dialog) {
            url.searchParams.set('dialog', competitor.dialog);
        }
        let response;
        try {
            response = await fetchWithTimeout(
                url,
                WINDBOT_REQUEST_TIMEOUT_MS,
                context.abortController.signal,
            );
        } catch (error) {
            if (context.abortController.signal.aborted) {
                throw context.abortController.signal.reason;
            }
            const requestError = new Error(
                error?.name === 'TimeoutError'
                    ? `${competitor.rankName} WindBot request timed out (${WINDBOT_REQUEST_TIMEOUT_MS / 1000} seconds)`
                    : `${competitor.rankName} WindBot request failed: ${error.message}`,
                { cause: error },
            );
            requestError.code = 'WINDBOT_REQUEST_FAILED';
            throw requestError;
        }
        if (!response.ok) {
            if (response.status === 404) {
                const isChallengeTarget = context.kind === 'challenge'
                    && competitor.source === 'target';
                const error = new Error(isChallengeTarget
                    ? `Challenger deck “${competitor.deck}” does not exist in the selected WindBot`
                    : `${competitor.rankName} deck “${competitor.deck}” does not exist in the selected WindBot; check that bot.conf matches its version`);
                error.code = 'WINDBOT_DECK_NOT_FOUND';
                throw error;
            }
            throw new Error(`${competitor.rankName} WindBot request returned HTTP ${response.status}`);
        }
    }

    async closeDuelRoom(context, password) {
        const srvpro = context.settings.srvpro;
        const url = makeHttpUrl(srvpro.host, srvpro.statusPort, '/api/message');
        url.searchParams.set('username', srvpro.username);
        url.searchParams.set('pass', srvpro.password);
        url.searchParams.set('kick', password);
        const response = await fetchSrvproApi(url, 'SRVPro close-room API ', 5000);
        const body = await response.text();
        this.assertServerInstance(context, getServerInstanceId(response));
        if (!response.ok || body.includes('\u5bc6\u7801\u9519\u8bef')) {
            throw new Error(`SRVPro refused to close the room: HTTP ${response.status} ${body}`);
        }
        if (!body.includes('kick ok') && !body.includes('room not found')) {
            throw new Error(`Cannot confirm SRVPro closed the room: ${body}`);
        }
    }

    async launchRoom(context, players) {
        const roomsPerSecond = getRoomLaunchRate(context);
        const joinDelayMs = SCHEDULE_POLL_MS / (roomsPerSecond * players.length);
        for (let attempt = 1; attempt <= WINDBOT_REQUEST_ATTEMPTS; attempt++) {
            const password = this.nextDuelPassword(context);
            try {
                await this.skynetRuntime.reserveRoom(context, players, password);
                for (const player of players) {
                    await this.addBot(context, player, password);
                    await sleep(joinDelayMs, context.abortController.signal);
                }
                return;
            } catch (error) {
                await this.skynetRuntime.stopRoom(context, password);
                let launchError = error;
                try {
                    await this.closeDuelRoom(context, password);
                } catch (cleanupError) {
                    if (!isAbortError(error)) {
                        launchError = new Error(
                            `${error.message}; Failed to close the challenge room: ${cleanupError.message}`,
                            { cause: error },
                        );
                        launchError.code = cleanupError.code || error.code;
                    }
                }
                if (launchError.code !== 'WINDBOT_REQUEST_FAILED') {
                    throw launchError;
                }
                if (attempt === WINDBOT_REQUEST_ATTEMPTS) {
                    const unavailableError = new Error(
                        `WindBot failed ${WINDBOT_REQUEST_ATTEMPTS} consecutive challenge-room requests: ${launchError.message}`,
                        { cause: launchError },
                    );
                    unavailableError.code = 'WINDBOT_UNAVAILABLE';
                    throw unavailableError;
                }
                await sleep(250, context.abortController.signal);
            }
        }
    }

    async launchMatchup(context, matchup) {
        const players = matchup.launchedGames % 2 === 0
            ? matchup.competitors
            : [...matchup.competitors].reverse();
        await this.launchRoom(context, players);
    }

    async launchRankingPair(context, entries) {
        const players = entries.map((entry) => entry.competitors[0]);
        if (Math.random() < 0.5) {
            players.reverse();
        }
        await this.launchRoom(context, players);
    }

    async launchTagGroup(context, entries) {
        const players = entries.map((entry) => entry.competitors[0]);
        for (let index = players.length - 1; index > 0; index--) {
            const swapIndex = Math.floor(Math.random() * (index + 1));
            [players[index], players[swapIndex]] = [players[swapIndex], players[index]];
        }
        await this.launchRoom(context, players);
    }

    async scheduleGames(context) {
        const { srvpro } = context.settings;
        const roomsPerSecond = getRoomLaunchRate(context);
        let consecutiveErrors = 0;
        let launchedGames = 0;
        while (
            context.kind === 'ranking'
            || context.kind === 'tag'
            || launchedGames < context.totalGames
        ) {
            if (context.gracefulStop) {
                return;
            }
            const pollStartedAt = Date.now();
            try {
                const roomCount = await this.getRoomCount(context);
                this.database.setRoomCount(context.id, roomCount);
                const availableRooms = Math.max(0, srvpro.maxRooms - roomCount);
                const toLaunch = Math.min(
                    availableRooms,
                    roomsPerSecond,
                    context.kind === 'ranking' || context.kind === 'tag'
                        ? roomsPerSecond
                        : context.totalGames - launchedGames,
                );

                for (let index = 0; index < toLaunch; index++) {
                    if (context.gracefulStop) {
                        return;
                    }
                    if (context.kind === 'ranking') {
                        const firstIndex = Math.floor(Math.random() * context.matchups.length);
                        let secondIndex = Math.floor(Math.random() * (context.matchups.length - 1));
                        if (secondIndex >= firstIndex) {
                            secondIndex++;
                        }
                        const entries = [context.matchups[firstIndex], context.matchups[secondIndex]];
                        await this.launchRankingPair(context, entries);
                        entries.forEach((entry) => { entry.launchedGames++; });
                        launchedGames++;
                        this.database.recordLaunch(context.id, entries.map((entry) => entry.id));
                        this.markChanged('progress');
                        continue;
                    }
                    if (context.kind === 'tag') {
                        const entries = Array.from({ length: 4 }, () => (
                            context.matchups[Math.floor(Math.random() * context.matchups.length)]
                        ));
                        await this.launchTagGroup(context, entries);
                        entries.forEach((entry) => { entry.launchedGames++; });
                        launchedGames++;
                        this.database.recordLaunch(context.id, entries.map((entry) => entry.id));
                        this.markChanged('progress');
                        continue;
                    }

                    let matchupIndex = context.nextMatchupIndex;
                    if (context.kind !== 'ranking') {
                        matchupIndex = -1;
                        for (let offset = 0; offset < context.matchups.length; offset++) {
                            const candidate = (context.nextMatchupIndex + offset) % context.matchups.length;
                            if (context.matchups[candidate].launchedGames < context.gamesPerMatchup) {
                                matchupIndex = candidate;
                                break;
                            }
                        }
                        if (matchupIndex === -1) {
                            break;
                        }
                    }
                    const matchup = context.matchups[matchupIndex];
                    await this.launchMatchup(context, matchup);
                    matchup.launchedGames++;
                    launchedGames++;
                    context.nextMatchupIndex = (matchupIndex + 1) % context.matchups.length;
                    this.database.recordLaunch(context.id, [matchup.id]);
                    this.markChanged('progress');
                }
                consecutiveErrors = 0;
            } catch (error) {
                if (context.abortController.signal.aborted) {
                    throw context.abortController.signal.reason;
                }
                if (
                    error?.code === 'WINDBOT_DECK_NOT_FOUND'
                    || error?.code === 'WINDBOT_UNAVAILABLE'
                ) {
                    throw error;
                }
                consecutiveErrors++;
                this.database.addEvent(context.id, 'warning', 'schedule-error', error.message);
                this.markChanged('schedule-error');
                if (consecutiveErrors >= 10) {
                    throw new Error('Stopped scheduling after 10 consecutive room-query or bot-launch failures');
                }
            }
            if (
                context.kind !== 'ranking'
                && context.kind !== 'tag'
                && launchedGames >= context.totalGames
            ) {
                return;
            }
            const remainingPollMs = Math.max(0, SCHEDULE_POLL_MS - (Date.now() - pollStartedAt));
            await sleep(remainingPollMs, context.abortController.signal);
        }
    }

    async waitForResults(context) {
        const deadline = Date.now() + SETTLE_TIMEOUT_MS;
        let finalScoreErrorRecorded = false;
        let emptyPolls = 0;
        while (context.gracefulStop || Date.now() < deadline) {
            const complete = context.matchups.every(
                (matchup) => (context.latestObserved.get(matchup.id) || 0) >= context.gamesPerMatchup,
            );
            if (!context.gracefulStop && complete) {
                return true;
            }
            let roomCount;
            try {
                roomCount = await this.getRoomCount(context);
                this.database.setRoomCount(context.id, roomCount);
            } catch (error) {
                if (context.abortController.signal.aborted) {
                    throw context.abortController.signal.reason;
                }
            }
            emptyPolls = roomCount === 0 ? emptyPolls + 1 : 0;
            // An accepted WindBot may not have joined yet; graceful stop confirms rooms remain empty.
            if (context.gracefulStop && emptyPolls < 2) {
                await sleep(SCHEDULE_POLL_MS, context.abortController.signal);
                continue;
            }
            if (roomCount === 0) {
                try {
                    if (context.kind !== 'tag') {
                        await this.queryScores(context, context.abortController.signal);
                    }
                } catch (error) {
                    if (context.abortController.signal.aborted) {
                        throw context.abortController.signal.reason;
                    }
                    if (!finalScoreErrorRecorded) {
                        this.recordScorePollError(context, error);
                        finalScoreErrorRecorded = true;
                    }
                    await sleep(SCHEDULE_POLL_MS, context.abortController.signal);
                    continue;
                }
                const completeAfterFinalQuery = context.matchups.every(
                    (matchup) => (context.latestObserved.get(matchup.id) || 0)
                        >= (context.gracefulStop ? matchup.launchedGames : context.gamesPerMatchup),
                );
                if (context.kind === 'tag' || completeAfterFinalQuery) {
                    return true;
                }
                this.database.addEvent(
                    context.id,
                    'warning',
                    'settle-empty-rooms',
                    'SRVPro room count is zero; run completed early, and some matches may be missing from the rankings',
                );
                this.markChanged('run-event');
                return true;
            }
            await sleep(SCHEDULE_POLL_MS, context.abortController.signal);
        }
        return false;
    }

    receiveRank(context, rank) {
        const normalized = normalizeRank(rank);
        const receivedAt = this.database.recordRank(context.id, normalized, rank);

        const rankMap = new Map(normalized);
        for (const matchup of context.matchups) {
            const observed = matchup.competitors.map(
                (competitor) => rankMap.get(competitor.rankName)?.games || 0,
            );
            context.latestObserved.set(matchup.id, Math.min(...observed));
        }
        this.markChanged('rank');
        return { matchedRunId: context.id, receivedAt };
    }

    stopRun(runId, reason = USER_STOP_REASON) {
        const context = [...this.contexts.values()].find((item) => item.id === runId);
        if (!context) {
            throw requestError('This run is not currently active', 409);
        }
        if (!context.stopReason) {
            clearTimeout(context.stopTimer);
            context.stopReason = reason;
            context.queryScoresBeforeStop = reason === USER_STOP_REASON;
            this.database.setRunStatus(runId, 'stopping');
            this.database.addEvent(runId, 'warning', 'stopping', reason);
            context.abortController.abort(new DOMException(reason, 'AbortError'));
            this.markChanged('stopping');
        }
        return this.database.getRun(runId);
    }

    scheduleStopRun(runId, minutes) {
        const context = [...this.contexts.values()].find((item) => item.id === runId);
        if (!context || context.finished || !context.running || context.stopReason || context.gracefulStop) {
            throw requestError('Scheduled stop can only be set for actively scheduling runs', 409);
        }
        if (context.kind !== 'ranking' && context.kind !== 'tag') {
            throw requestError('Scheduled stop only applies to unbounded modes');
        }
        if (!Number.isInteger(minutes) || minutes < 1 || minutes > 10080) {
            throw requestError('Scheduled stop duration must be an integer from 1 to 10080 minutes');
        }
        const stopAt = new Date(Date.now() + minutes * 60_000).toISOString();
        this.database.setRunStopAt(runId, stopAt);
        clearTimeout(context.stopTimer);
        context.stopTimer = setTimeout(() => {
            if (!context.finished && !context.stopReason && !context.gracefulStop) {
                this.gracefulStopRun(runId, 'Scheduled stop reached: no new rooms; waiting for active rooms before finalizing results');
            }
        }, minutes * 60_000);
        context.stopTimer.unref();
        this.database.addEvent(runId, 'info', 'scheduled-stop', `Graceful stop scheduled in ${minutes} minutes`);
        this.markChanged('scheduled-stop');
        return this.database.getRun(runId);
    }

    gracefulStopRun(runId, reason = 'Graceful stop requested: no new rooms; waiting for active rooms before finalizing results') {
        const context = [...this.contexts.values()].find((item) => item.id === runId);
        if (!context || context.finished) {
            throw requestError('This run is not currently active', 409);
        }
        if (!context.running) {
            return this.stopRun(runId);
        }
        if (!context.gracefulStop && !context.stopReason) {
            clearTimeout(context.stopTimer);
            context.gracefulStop = true;
            this.database.setRunStatus(runId, 'settling');
            this.database.addEvent(runId, 'info', 'settling', reason);
            this.markChanged('settling');
        }
        return this.database.getRun(runId);
    }

    updateRunNote(runId, note) {
        if (typeof note !== 'string' || note.length > 200) {
            throw requestError('Note must be text of at most 200 characters', 400);
        }
        if (!this.database.setRunNote(runId, note.trim())) {
            throw requestError('Run record does not exist', 404);
        }
        this.markChanged('note');
        return this.database.getRun(runId);
    }

    deleteRun(runId) {
        const run = this.database.getRun(runId);
        if (!run) {
            throw requestError('Run record does not exist', 404);
        }
        if (!TERMINAL_RUN_STATUSES.has(run.status)) {
            throw requestError('Active runs cannot be deleted; stop the run first', 409);
        }
        if (!this.database.deleteRun(runId)) {
            throw requestError('Run record does not exist', 404);
        }
        this.revisions.runs++;
        return run;
    }

    async finish(context, status, reason, error = null) {
        if (context.finished) {
            return;
        }
        context.finished = true;
        clearTimeout(context.stopTimer);
        context.running = false;
        if (!context.abortController.signal.aborted) {
            context.abortController.abort(new DOMException('Run ended', 'AbortError'));
        }
        if (!await this.skynetRuntime.stopContext(context)) {
            status = 'failed';
            error = 'SKYNET did not confirm owned process termination within the cleanup deadline';
            reason = error;
        }
        await context.scorePoller;
        if (status === 'stopped' && context.queryScoresBeforeStop && context.scorePoller) {
            try {
                await this.queryScores(context);
            } catch (error) {
                this.recordScorePollError(context, error);
            }
        }
        this.database.setRunStatus(context.id, status, {
            error,
            finishedAt: new Date().toISOString(),
            stopReason: reason,
        });
        if (status !== 'stopped' || (context.gracefulStop && !context.stopReason)) {
            this.database.addEvent(
                context.id,
                status === 'failed' ? 'error' : 'info',
                status,
                reason,
            );
        }
        if (this.contexts.get(context.srvproId) === context) {
            this.contexts.delete(context.srvproId);
        }
        if (this.contexts.size === 0) {
            this.stopLocalWindBots();
        }
        this.markChanged(status);
        if (!(status === 'stopped' && context.stopReason === USER_STOP_REASON)) {
            const notification = (async () => {
                try {
                    await this.notifications.send(this.database.getRun(context.id));
                } catch {
                    // A finished run may be deleted while its notification is pending.
                    if (!this.database.getRun(context.id)) return;
                    // Delivery errors can contain secret endpoint URLs; keep task logs generic.
                    this.database.addEvent(context.id, 'warning', 'notification-error', 'Run finished, but notification delivery failed; check notification settings and connectivity');
                    this.markChanged('notification-error');
                }
            })();
            this.pendingNotifications.add(notification);
            try {
                await notification;
            } finally {
                this.pendingNotifications.delete(notification);
            }
        }
    }

    markChanged(reason) {
        if (reason === 'settings' || reason === 'decks') {
            this.revisions.system++;
            return;
        }
        this.revisions.active++;
        if (
            reason !== 'progress'
            && reason !== 'rank'
            && reason !== 'run-event'
            && reason !== 'schedule-error'
            && reason !== 'score-poll-error'
        ) {
            this.revisions.runs++;
        }
    }

    async shutdown() {
        const contexts = [...this.contexts.values()];
        if (contexts.length === 0) {
            this.stopLocalWindBots();
        }
        for (const context of contexts) {
            this.stopRun(context.id, 'Arena is shutting down');
        }
        await Promise.all(contexts.map((context) => context.done));
        await this.skynetRuntime.shutdown();
        await Promise.all(this.pendingNotifications);
    }
}

module.exports = {
    ArenaService,
    getRoomLaunchRate,
    SCORE_POLL_MS,
    requestError,
};
