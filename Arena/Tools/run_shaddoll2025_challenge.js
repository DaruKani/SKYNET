'use strict';

const NORMAL_AI_LEVEL = 3;
const TARGET_DECK = 'Shaddoll2025';
const TERMINAL_STATUSES = new Set(['completed', 'failed', 'interrupted', 'stopped']);

function parseArguments(argv) {
    const options = {
        arenaUrl: 'http://127.0.0.1:3000',
        gamesPerOpponent: 1,
        opponents: [],
        pollSeconds: 5,
        preflightOnly: false,
    };
    for (let index = 0; index < argv.length; index += 1) {
        const argument = argv[index];
        const next = () => {
            index += 1;
            if (index >= argv.length) throw new Error(`${argument} requires a value`);
            return argv[index];
        };
        if (argument === '--url') options.arenaUrl = next().replace(/\/$/, '');
        else if (argument === '--games') options.gamesPerOpponent = Number(next());
        else if (argument === '--opponent') options.opponents.push(next());
        else if (argument === '--poll-seconds') options.pollSeconds = Number(next());
        else if (argument === '--preflight-only') options.preflightOnly = true;
        else throw new Error(`Unknown argument: ${argument}`);
    }
    if (!Number.isInteger(options.gamesPerOpponent)
        || options.gamesPerOpponent < 1
        || options.gamesPerOpponent > 10000) {
        throw new Error('--games must be an integer from 1 to 10000');
    }
    if (!Number.isInteger(options.pollSeconds)
        || options.pollSeconds < 1
        || options.pollSeconds > 60) {
        throw new Error('--poll-seconds must be an integer from 1 to 60');
    }
    return options;
}

function selectNormalOpponents(currentDecks, requestedOpponents = []) {
    const catalog = new Map(currentDecks.map((item) => [item.deck, item]));
    const target = catalog.get(TARGET_DECK);
    if (!target) {
        throw new Error(`${TARGET_DECK} is missing from Arena's candidate bot catalog`);
    }
    if (target.aiLevel !== NORMAL_AI_LEVEL) {
        throw new Error(`${TARGET_DECK} must be classified as AI_LV${NORMAL_AI_LEVEL} in bot.conf`);
    }
    if (requestedOpponents.length > 0) {
        const unique = [...new Set(requestedOpponents)];
        const missing = unique.filter((deck) => !catalog.has(deck));
        if (missing.length > 0) {
            throw new Error(`Opponent decks are missing from Arena's catalog: ${missing.join(', ')}`);
        }
        const notNormal = unique.filter((deck) => catalog.get(deck).aiLevel !== NORMAL_AI_LEVEL);
        if (notNormal.length > 0) {
            throw new Error(`Opponent decks are not AI_LV${NORMAL_AI_LEVEL} (Normal): ${notNormal.join(', ')}`);
        }
        return unique.filter((deck) => deck !== TARGET_DECK);
    }
    return currentDecks
        .filter((item) => item.aiLevel === NORMAL_AI_LEVEL && item.deck !== TARGET_DECK)
        .map((item) => item.deck);
}

async function requestJson(arenaUrl, pathname, options = {}) {
    const response = await fetch(`${arenaUrl}${pathname}`, {
        ...options,
        headers: {
            'Content-Type': 'application/json',
            ...(options.headers || {}),
        },
    });
    const text = await response.text();
    let body = null;
    if (text) {
        try {
            body = JSON.parse(text);
        } catch {
            throw new Error(`${pathname} returned invalid JSON (HTTP ${response.status})`);
        }
    }
    if (!response.ok) {
        throw new Error(body?.error || `${pathname} returned HTTP ${response.status}`);
    }
    return body;
}

function printRunSummary(run) {
    const summary = run.matchups.map((matchup) => {
        const target = matchup.competitors.find((item) => item.source === 'target');
        const opponent = matchup.competitors.find((item) => item.source === 'opponent');
        return {
            opponent: matchup.label,
            observed: matchup.observedGames || 0,
            targetWins: target?.win || 0,
            opponentWins: opponent?.win || 0,
            targetDisconnects: target?.flee || 0,
        };
    });
    console.table(summary);
}

async function main(argv = process.argv.slice(2)) {
    const options = parseArguments(argv);
    await requestJson(options.arenaUrl, '/api/system');
    const refresh = await requestJson(options.arenaUrl, '/api/decks/refresh', { method: 'POST' });
    const decks = await requestJson(options.arenaUrl, '/api/decks');
    const opponents = selectNormalOpponents(decks.currentDecks, options.opponents);
    if (opponents.length === 0) {
        throw new Error('No AI_LV3 (Normal) opponent remains after excluding Shaddoll2025');
    }

    console.log(`Shaddoll2025 challenge: ${opponents.length} Normal opponent(s)`);
    console.log(opponents.join(', '));
    const challenge = refresh.configuration?.modes?.challenge;
    if (!challenge?.valid) {
        const issues = challenge?.issues?.length
            ? challenge.issues.join('; ')
            : 'Arena did not return challenge configuration status';
        const error = new Error(`Arena challenge preflight is incomplete: ${issues}`);
        error.exitCode = 2;
        throw error;
    }
    if (options.preflightOnly) {
        console.log('Arena challenge preflight passed; no run was created.');
        return null;
    }

    const created = await requestJson(options.arenaUrl, '/api/runs', {
        body: JSON.stringify({
            challengerVersion: 'current',
            decks: opponents,
            gamesPerMatchup: options.gamesPerOpponent,
            kind: 'challenge',
            targetDeck: TARGET_DECK,
        }),
        method: 'POST',
    });
    const runId = created.run.id;
    console.log(`Arena run created: ${runId}`);
    while (true) {
        const result = await requestJson(options.arenaUrl, `/api/runs/${encodeURIComponent(runId)}`);
        const run = result.run;
        console.log(`[${new Date().toISOString()}] ${run.status}: ${run.launchedGames}/${run.totalGames} launched`);
        if (TERMINAL_STATUSES.has(run.status)) {
            printRunSummary(run);
            if (run.status !== 'completed') {
                throw new Error(`Arena run ended with status ${run.status}: ${run.error || run.stopReason || 'no detail'}`);
            }
            return run;
        }
        await new Promise((resolve) => setTimeout(resolve, options.pollSeconds * 1000));
    }
}

if (require.main === module) {
    main().catch((error) => {
        console.error(error.message);
        process.exitCode = error.exitCode || 1;
    });
}

module.exports = {
    NORMAL_AI_LEVEL,
    TARGET_DECK,
    parseArguments,
    printRunSummary,
    selectNormalOpponents,
};
