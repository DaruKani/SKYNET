'use strict';

const { DEFAULT_WEBHOOK_TEMPLATE, renderWebhookTemplate, renderWebhookUrl } = require('./notifications');
const { defaultSkynetSettings, normalizeSkynetSettings } = require('./skynet-config');

function createDefaultArenaSettings() {
    return {
        notifications: { mode: 'off', webhookMethod: 'POST', webhookUrl: '', webhookTemplate: DEFAULT_WEBHOOK_TEMPLATE, webhookHeaders: '' },
        srvpros: [{
            duelPort: 7911,
            host: '',
            id: 'srvpro-1',
            maxRooms: 100,
            name: 'SRVPro 1',
            password: '',
            roomsPerSecond: 1,
            statusPort: 7922,
            username: '',
        }],
        windbots: {
            current: {
                botConfPath: '',
                botConfText: '',
                botConfUrl: '',
                host: '',
                mode: 'local',
                port: 2399,
                runtimeDir: '',
                skynet: defaultSkynetSettings(),
            },
            old: {
                botConfPath: '',
                botConfText: '',
                botConfUrl: '',
                host: '',
                mode: 'local',
                port: 2398,
                runtimeDir: '',
                skynet: defaultSkynetSettings(),
            },
        },
    };
}

function readInteger(value, label, minimum, maximum) {
    const number = Number(value);
    if (!Number.isInteger(number) || number < minimum || number > maximum) {
        throw new Error(`${label} must be an integer from ${minimum} to ${maximum}`);
    }
    return number;
}

function readWindBot(input, existing, label) {
    const source = input || {};
    const mode = source.mode;
    if (!['local', 'remote', 'skynet'].includes(mode)) {
        throw new Error(`${label}execution mode is invalid`);
    }
    const result = {
        ...existing,
        mode,
        port: readInteger(source.port ?? existing.port, `${label}service port`, 1, 65535),
    };
    if (mode === 'local') {
        result.botConfPath = String(source.botConfPath || '').trim();
        result.runtimeDir = String(source.runtimeDir || '').trim();
    } else if (mode === 'skynet') {
        result.skynet = normalizeSkynetSettings(source.skynet || existing.skynet);
    } else {
        result.botConfText = String(source.botConfText || '');
        result.botConfUrl = String(source.botConfUrl || '').trim();
        result.host = String(source.host || '').trim();
        if (result.botConfUrl) {
            let url;
            try {
                url = new URL(result.botConfUrl);
            } catch {
                throw new Error(`${label}bot.conf URL is invalid`);
            }
            if (!['http:', 'https:'].includes(url.protocol)) {
                throw new Error(`${label}bot.conf URL supports only HTTP or HTTPS`);
            }
            if (result.botConfUrl.length > 2000) {
                throw new Error(`${label}bot.conf URL must not exceed 2000 characters`);
            }
        }
    }
    return result;
}

function validateAndMergeArenaSettings(input, existing) {
    if (!input || typeof input !== 'object') {
        throw new Error('Invalid settings');
    }
    const previous = existing || createDefaultArenaSettings();
    if (!Array.isArray(input.srvpros) || input.srvpros.length === 0) {
        throw new Error('Configure at least one SRVPro instance');
    }
    if (input.srvpros.length > 100) {
        throw new Error('At most 100 SRVPro instances are supported');
    }
    const previousSrvpros = new Map(previous.srvpros.map((srvpro) => [srvpro.id, srvpro]));
    const ids = new Set();
    const managementEndpoints = new Set();
    const srvpros = input.srvpros.map((source, index) => {
        const id = String(source?.id || '').trim();
        const name = String(source?.name || '').trim();
        if (!/^[A-Za-z0-9_-]{1,64}$/.test(id)) {
            throw new Error(`SRVPro instance ${index + 1} has an invalid ID`);
        }
        if (ids.has(id)) {
            throw new Error(`Duplicate SRVPro instance ID: ${id}`);
        }
        ids.add(id);
        if (!name) {
            throw new Error(`SRVPro instance ${index + 1} name must not be empty`);
        }
        if (name.length > 100) {
            throw new Error(`SRVPro instance names must not exceed 100 characters: ${name}`);
        }
        const host = String(source.host || '').trim();
        const statusPort = readInteger(source.statusPort, `${name} management port`, 1, 65535);
        const managementEndpoint = `${host.toLocaleLowerCase()}:${statusPort}`;
        if (host && managementEndpoints.has(managementEndpoint)) {
            throw new Error(`Duplicate SRVPro management endpoint: ${host}:${statusPort}`);
        }
        managementEndpoints.add(managementEndpoint);
        const existingSrvpro = previousSrvpros.get(id);
        const password = typeof source.password === 'string' && source.password !== ''
            ? source.password
            : existingSrvpro?.password || '';
        return {
            duelPort: readInteger(source.duelPort, `${name} duel port`, 1, 65535),
            host,
            id,
            maxRooms: readInteger(source.maxRooms, `${name} room limit`, 1, 100000),
            name,
            password,
            roomsPerSecond: readInteger(source.roomsPerSecond, `${name} rooms per second`, 1, 100),
            statusPort,
            username: String(source.username || '').trim(),
        };
    });

    const notifications = {
        ...(previous.notifications || createDefaultArenaSettings().notifications),
        ...input.notifications,
    };
    if (!['off', 'frontend', 'webhook'].includes(notifications.mode)) {
        throw new Error('Invalid notification mode');
    }
    notifications.webhookMethod ??= 'POST';
    if (!['GET', 'POST'].includes(notifications.webhookMethod)) throw new Error('Webhook method must be GET or POST');
    notifications.webhookUrl = String(notifications.webhookUrl ?? '').trim();
    notifications.webhookTemplate = String(notifications.webhookTemplate ?? '');
    notifications.webhookHeaders = String(notifications.webhookHeaders ?? '').trim();
    if (notifications.webhookUrl) {
        let url;
        try {
            url = new URL(renderWebhookUrl(notifications.webhookUrl, {}));
        } catch { throw new Error('Webhook: Invalid URL or template variable'); }
        if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || notifications.webhookUrl.length > 2000) {
            throw new Error('Webhook URL must use HTTP(S), contain no embedded credentials, and be at most 2000 characters');
        }
    }
    if (notifications.mode === 'webhook' && !notifications.webhookUrl) {
        throw new Error('Webhook URL must not be empty');
    }
    if (notifications.webhookTemplate.length > 20000 || notifications.webhookHeaders.length > 10000) {
        throw new Error('Webhook JSON template or headers exceed the size limit');
    }
    if (notifications.mode === 'webhook' && notifications.webhookMethod === 'POST' && !notifications.webhookTemplate.trim()) {
        throw new Error('Request body template must not be empty');
    }
    if (notifications.webhookTemplate.trim()) {
        try {
            const body = renderWebhookTemplate(notifications.webhookTemplate, {});
            if (!body || typeof body !== 'object' || Array.isArray(body)) throw new Error();
        } catch {
            throw new Error('Webhook template must be a JSON object using supported variables only');
        }
    }
    try {
        const headers = JSON.parse(notifications.webhookHeaders || '{}');
        if (!headers || typeof headers !== 'object' || Array.isArray(headers)) throw new Error();
        for (const [key, value] of Object.entries(headers)) {
            if (typeof value !== 'string' || /^(host|content-length|content-type|connection|transfer-encoding)$/i.test(key)) throw new Error();
            new Headers({ [key]: value });
        }
    } catch {
        throw new Error('Webhook headers must be a JSON object with string values and cannot override Host, Content-Type or transport-control headers');
    }
    const settings = {
        notifications: {
            mode: notifications.mode,
            webhookMethod: notifications.webhookMethod,
            webhookUrl: notifications.webhookUrl,
            webhookTemplate: notifications.webhookTemplate,
            webhookHeaders: notifications.webhookHeaders,
        },
        srvpros,
        windbots: {
            current: readWindBot(input.windbots?.current, previous.windbots.current, 'Candidate agent '),
            old: readWindBot(input.windbots?.old, previous.windbots.old, 'Baseline agent '),
        },
    };
    const old = settings.windbots.old;
    const oldConfigured = old.mode === 'skynet' ? false : old.mode === 'local'
        ? old.runtimeDir !== '' && old.botConfPath !== ''
        : old.host !== '' && (old.botConfText.trim() !== '' || old.botConfUrl !== '');
    if (
        oldConfigured
        && sameWindBotEndpoint(settings.windbots.current, old)
    ) {
        throw new Error('Current and baseline WindBot must not use the same service endpoint');
    }
    return settings;
}

function getWindBotEndpoint(windbot) {
    if (windbot.mode === 'skynet') return null;
    const host = windbot.mode === 'local' ? '127.0.0.1' : windbot.host;
    return `${host}:${windbot.port}`;
}

function sameWindBotEndpoint(left, right) {
    return left.mode !== 'skynet' && right.mode !== 'skynet'
        && getWindBotEndpoint(left) === getWindBotEndpoint(right);
}

function getPublicArenaSettings(settings, updatedAt) {
    const result = structuredClone(settings);
    const defaultRoomsPerSecond = createDefaultArenaSettings().srvpros[0].roomsPerSecond;
    const secretStatus = { srvpros: {} };
    result.notifications = { ...createDefaultArenaSettings().notifications, ...result.notifications };
    for (const srvpro of result.srvpros) {
        const source = settings.srvpros.find((item) => item.id === srvpro.id);
        secretStatus.srvpros[srvpro.id] = {
            passwordConfigured: typeof source?.password === 'string' && source.password !== '',
        };
        srvpro.roomsPerSecond ??= defaultRoomsPerSecond;
        srvpro.password = '';
        delete srvpro.accessKey;
        delete srvpro.maxRankNames;
        delete srvpro.rankPostPath;
    }
    delete result.scheduler;
    delete result.development;
    for (const instance of Object.values(result.windbots)) {
        instance.botConfUrl ||= '';
        instance.skynet = { ...defaultSkynetSettings(), ...instance.skynet };
    }
    return {
        secretStatus,
        settings: result,
        updatedAt,
    };
}

module.exports = {
    createDefaultArenaSettings,
    getPublicArenaSettings,
    getWindBotEndpoint,
    sameWindBotEndpoint,
    validateAndMergeArenaSettings,
};
