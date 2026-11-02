'use strict';

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { sameWindBotEndpoint } = require('./arena-settings');
const { skynetCatalog, validateSkynetRuntime } = require('./skynet-config');

// WindBot WriteUnicode(Username, 20) reserves one UTF-16 unit for the terminator.
const MAX_PARTICIPANT_NAME_LENGTH = 19;

function participantName(name) {
    if (name.length <= MAX_PARTICIPANT_NAME_LENGTH) return name;
    const suffix = '-' + crypto.createHash('sha256').update(name, 'utf8').digest('hex').slice(0, 8);
    let prefix = '';
    for (const character of name) {
        if (prefix.length + character.length > MAX_PARTICIPANT_NAME_LENGTH - suffix.length) break;
        prefix += character;
    }
    return prefix + suffix;
}

function parseBotConfig(content) {
    const bots = new Map();
    const blocks = content.split(/(?=^!)/m);
    for (const block of blocks) {
        const match = block.match(
            /^!([^\r\n]+)[\s\S]*?^Name=(?:'[^'\r\n]+'|\S+)\s+Deck=(?:'([^'\r\n]+)'|(\S+))\s+Dialog=(\S+)/m,
        );
        if (!match || /\bSELECT_DECKFILE\b/.test(block)) {
            continue;
        }

        const deck = match[2] || match[3];
        const aiLevel = block.match(/\bAI_LV(\d+)\b/);
        bots.set(deck, {
            aiLevel: aiLevel ? Number(aiLevel[1]) : null,
            deck,
            dialog: match[4],
            label: match[1],
        });
    }
    return bots;
}

function loadBotConfigText(instance, label) {
    if (instance.mode === 'skynet') {
        return JSON.stringify(instance.skynet);
    }
    if (instance.mode === 'remote') {
        if (instance.botConfText.trim() === '') {
            throw new Error(`${label} remote mode requires bot.conf content`);
        }
        return instance.botConfText;
    }
    if (!fs.existsSync(instance.botConfPath)) {
        throw new Error(`${label} bot.conf does not exist: ${instance.botConfPath}`);
    }
    return fs.readFileSync(instance.botConfPath, 'utf8');
}

function loadBotsFromInstance(instance, label) {
    if (instance.mode === 'skynet') return skynetCatalog(instance);
    return parseBotConfig(loadBotConfigText(instance, label));
}

function getRegressionCatalog(settings) {
    const currentBots = loadBotsFromInstance(settings.windbots.current, 'Current');
    const oldBots = loadBotsFromInstance(settings.windbots.old, 'Baseline');
    return [...currentBots.values()]
        .filter((bot) => oldBots.has(bot.deck))
        .map((current) => ({
            current,
            deck: current.deck,
            old: oldBots.get(current.deck),
        }))
        .sort((left, right) => {
            if (left.current.aiLevel === null) {
                return right.current.aiLevel === null ? left.deck.localeCompare(right.deck) : 1;
            }
            if (right.current.aiLevel === null) {
                return -1;
            }
            return right.current.aiLevel - left.current.aiLevel
                || left.deck.localeCompare(right.deck);
        });
}

function getCurrentCatalog(settings) {
    return [...loadBotsFromInstance(settings.windbots.current, 'Current').values()]
        .sort((left, right) => {
            if (left.aiLevel === null) {
                return right.aiLevel === null ? left.deck.localeCompare(right.deck) : 1;
            }
            if (right.aiLevel === null) {
                return -1;
            }
            return right.aiLevel - left.aiLevel || left.deck.localeCompare(right.deck);
        });
}

function getOldCatalog(settings) {
    return [...loadBotsFromInstance(settings.windbots.old, 'Baseline').values()]
        .sort((left, right) => {
            if (left.aiLevel === null) {
                return right.aiLevel === null ? left.deck.localeCompare(right.deck) : 1;
            }
            if (right.aiLevel === null) {
                return -1;
            }
            return right.aiLevel - left.aiLevel || left.deck.localeCompare(right.deck);
        });
}

function validateWindBotRuntime(instance, label) {
    if (instance.mode === 'skynet') {
        validateSkynetRuntime(instance);
        return;
    }
    if (instance.mode === 'local') {
        const exePath = path.join(instance.runtimeDir, 'WindBot.exe');
        if (!fs.existsSync(exePath)) {
            throw new Error(`${label} executable does not exist: ${exePath}`);
        }
        return;
    }
    if (instance.host.trim() === '') {
        throw new Error(`${label} remote service host must not be empty`);
    }
}

function getDialog(instance, dialog) {
    if (instance.mode === 'skynet') return null;
    if (instance.mode === 'remote') {
        return dialog;
    }
    return fs.existsSync(path.join(instance.runtimeDir, 'Dialogs', `${dialog}.json`))
        ? dialog
        : null;
}

function makeCompetitor(instance, bot, source, slot, rankName) {
    if (instance.mode === 'skynet' && !bot.skynet) {
        throw new Error(`SKYNET profile is not configured: ${bot.deck}`);
    }
    return {
        botLabel: bot.label,
        deck: bot.deck,
        dialog: getDialog(instance, bot.dialog),
        endpointHost: instance.mode !== 'remote' ? '127.0.0.1' : instance.host,
        endpointPort: instance.port,
        executionMode: instance.mode,
        rankName: participantName(rankName || `${source === 'current' ? 'C' : 'B'}-${bot.label}`),
        runtimeDir: instance.runtimeDir,
        slot,
        source,
        ...(bot.skynet ? { skynet: structuredClone(bot.skynet) } : {}),
    };
}

function validateRankNames(matchups, settings) {
    const competitorsByRankName = new Map();
    for (const competitor of matchups.flatMap((matchup) => matchup.competitors)) {
        const competitors = competitorsByRankName.get(competitor.rankName) || [];
        competitors.push(competitor);
        competitorsByRankName.set(competitor.rankName, competitors);
    }
    const duplicateRankNames = [...competitorsByRankName]
        .filter(([, competitors]) => (
            competitors.length > 1 && !competitors.every((item) => item.source === 'target')
        ))
        .map(([name]) => name);
    if (duplicateRankNames.length > 0) {
        throw new Error(`This run generated duplicate ranking names: ${
            duplicateRankNames.map((name) => `“${name}”`).join(', ')
        }`);
    }
    const rankNames = [...competitorsByRankName.keys()];
    const overlongRankNames = rankNames.filter((name) => name.length > MAX_PARTICIPANT_NAME_LENGTH);
    if (overlongRankNames.length > 0) {
        throw new Error(`This run generated ranking names exceeding ${MAX_PARTICIPANT_NAME_LENGTH} UTF-16 units: ${
            overlongRankNames.map((name) => `“${name}”`).join(', ')
        }`);
    }
}

function buildRegressionMatchups(settings, requestedDecks) {
    const currentInstance = settings.windbots.current;
    const oldInstance = settings.windbots.old;
    if (sameWindBotEndpoint(currentInstance, oldInstance)) {
        throw new Error('Current and baseline WindBot must not use the same service endpoint');
    }
    validateWindBotRuntime(currentInstance, 'Current');
    validateWindBotRuntime(oldInstance, 'Baseline');

    const catalog = getRegressionCatalog(settings);
    if (catalog.length === 0) {
        throw new Error('Current and baseline catalogs have no shared runnable decks');
    }

    const catalogByDeck = new Map(catalog.map((item) => [item.deck, item]));
    const requestedDeckSet = requestedDecks?.length ? new Set(requestedDecks) : null;
    const missing = requestedDeckSet
        ? [...requestedDeckSet].filter((deck) => !catalogByDeck.has(deck))
        : [];
    if (missing.length > 0) {
        throw new Error(`These decks are not present in both catalogs: ${missing.join(', ')}`);
    }
    const selectedDecks = catalog
        .filter((item) => (
            requestedDeckSet
                ? requestedDeckSet.has(item.deck)
                : item.current.aiLevel !== 1
        ))
        .map((item) => item.deck);
    if (selectedDecks.length === 0) {
        throw new Error('No eligible shared decks; bulk selection excludes LV1');
    }

    const matchups = selectedDecks.map((deck) => {
        const pair = catalogByDeck.get(deck);
        return {
            aiLevel: pair.current.aiLevel,
            label: deck,
            competitors: [
                makeCompetitor(currentInstance, pair.current, 'current', 1),
                makeCompetitor(oldInstance, pair.old, 'old', 2),
            ],
        };
    });
    validateRankNames(matchups, settings);
    return matchups;
}

function buildChallengeMatchups(settings, targetDeckInput, requestedDecks, challengerVersion = 'current') {
    if (!['current', 'old'].includes(challengerVersion)) {
        throw new Error('Invalid challenger version');
    }
    const targetInstance = settings.windbots[challengerVersion];
    const opponentInstance = settings.windbots.current;
    validateWindBotRuntime(targetInstance, challengerVersion === 'current' ? 'Current' : 'Baseline');
    if (challengerVersion === 'old') {
        validateWindBotRuntime(opponentInstance, 'Current');
    }
    const requestedTargetDeck = String(targetDeckInput || '').trim();
    if (!requestedTargetDeck) {
        throw new Error('Challenger deck name must not be empty');
    }

    const opponentCatalog = getCurrentCatalog(settings);
    const opponentCatalogByDeck = new Map(opponentCatalog.map((bot) => [bot.deck, bot]));
    const targetCatalog = challengerVersion === 'current' ? opponentCatalog : getOldCatalog(settings);
    const targetCatalogByDeck = new Map(targetCatalog.map((bot) => [bot.deck, bot]));
    const targetDeck = targetCatalog.find((bot) => (
        requestedTargetDeck === `${bot.deck} — ${bot.label}`
    ))?.deck || requestedTargetDeck;
    let opponentDecks;
    if (requestedDecks?.length) {
        opponentDecks = [...new Set(requestedDecks.map((deck) => deck.trim()))];
        const missing = opponentDecks.filter((deck) => !opponentCatalogByDeck.has(deck));
        if (missing.length > 0) {
            throw new Error(`These opponents are not in the current catalog: ${missing.join(', ')}`);
        }
    } else {
        opponentDecks = opponentCatalog.filter((bot) => bot.aiLevel !== 1).map((bot) => bot.deck);
        if (!opponentDecks.includes(targetDeck)) {
            opponentDecks.push(targetDeck);
        }
    }
    if (opponentDecks.length === 0) {
        throw new Error('At least one opponent deck is required');
    }

    const targetBot = targetCatalogByDeck.get(targetDeck) || {
        aiLevel: null,
        deck: targetDeck,
        dialog: 'default',
        label: targetDeck,
    };
    const matchups = opponentDecks.map((opponentDeck) => {
        const opponent = opponentCatalogByDeck.get(opponentDeck) || {
            aiLevel: null,
            deck: opponentDeck,
            dialog: 'default',
            label: opponentDeck,
        };
        return {
            aiLevel: opponent.aiLevel,
            label: opponentDeck,
            competitors: [
                makeCompetitor(
                    targetInstance,
                    targetBot,
                    'target',
                    1,
                    targetInstance.mode === 'skynet' ? `T-${targetBot.label}` : targetBot.deck,
                ),
                makeCompetitor(
                    opponentInstance,
                    opponent,
                    'opponent',
                    2,
                    opponentCatalogByDeck.has(opponentDeck) ? opponent.label : `Opp-${opponent.deck}`,
                ),
            ],
        };
    });
    validateRankNames(matchups, settings);
    return matchups;
}

function buildRankingEntries(settings, requestedDecks) {
    const instance = settings.windbots.current;
    validateWindBotRuntime(instance, 'Current');
    const catalog = getCurrentCatalog(settings);
    const catalogByDeck = new Map(catalog.map((bot) => [bot.deck, bot]));
    const selectedDecks = requestedDecks?.length
        ? [...new Set(requestedDecks.map((deck) => deck.trim()))]
        : catalog.filter((bot) => bot.aiLevel !== 1).map((bot) => bot.deck);
    const missing = selectedDecks.filter((deck) => !catalogByDeck.has(deck));
    if (missing.length > 0) {
        throw new Error(`These decks are not in the current catalog: ${missing.join(', ')}`);
    }
    if (selectedDecks.length < 2) {
        throw new Error('Ranking requires at least two decks');
    }

    const entries = selectedDecks.map((deck) => {
        const bot = catalogByDeck.get(deck);
        return {
            aiLevel: bot.aiLevel,
            label: deck,
            competitors: [
                makeCompetitor(
                    instance,
                    bot,
                    'current',
                    1,
                    bot.label,
                ),
            ],
        };
    });
    validateRankNames(entries, settings);
    return entries;
}

function buildTagEntries(settings, requestedDecks) {
    const instance = settings.windbots.current;
    if (instance.mode === 'skynet') {
        throw new Error('SKYNET tag-duel support has not been validated; use regression, challenge or ranking');
    }
    validateWindBotRuntime(instance, 'Current');
    const catalog = getCurrentCatalog(settings);
    const catalogByDeck = new Map(catalog.map((bot) => [bot.deck, bot]));
    const selectedDecks = requestedDecks?.length
        ? [...new Set(requestedDecks.map((deck) => deck.trim()))]
        : catalog.filter((bot) => bot.aiLevel !== 1).map((bot) => bot.deck);
    const missing = selectedDecks.filter((deck) => !catalogByDeck.has(deck));
    if (missing.length > 0) {
        throw new Error(`These decks are not in the current catalog: ${missing.join(', ')}`);
    }
    if (selectedDecks.length === 0) {
        throw new Error('Tag smoke tests require at least one deck');
    }

    const entries = selectedDecks.map((deck) => {
        const bot = catalogByDeck.get(deck);
        return {
            aiLevel: bot.aiLevel,
            competitors: [makeCompetitor(instance, bot, 'current', 1, bot.label)],
            label: deck,
        };
    });
    validateRankNames(entries, settings);
    return entries;
}

function inspectConfiguration(settings, selectedSrvpro = settings.srvpro || settings.srvpros?.[0]) {
    const baseIssues = [];
    const srvpro = selectedSrvpro || {};
    for (const [label, value] of [
        ['SRVPro host', srvpro.host],
        ['SRVPro admin username', srvpro.username],
        ['SRVPro admin password', srvpro.password],
    ]) {
        if (typeof value !== 'string' || value.trim() === '') {
            baseIssues.push(`${label} is not configured`);
        }
    }

    const inspectInstance = (label, instance) => {
        const issues = [];
        if (instance.mode === 'skynet') {
            try { validateSkynetRuntime(instance); } catch (error) { issues.push(error.message); }
        } else if (instance.mode === 'local') {
            if (!instance.runtimeDir) {
                issues.push(`${label} WindBot runtime directory is not configured`);
            } else {
                const executablePath = path.join(instance.runtimeDir, 'WindBot.exe');
                if (!fs.existsSync(executablePath)) {
                    issues.push(`${label} WindBot.exe does not exist: ${executablePath}`);
                }
            }
            if (!instance.botConfPath) {
                issues.push(`${label} bot.conf path is not configured`);
            } else if (!fs.existsSync(instance.botConfPath)) {
                issues.push(`${label} bot.conf does not exist: ${instance.botConfPath}`);
            }
        } else {
            if (!instance.host) {
                issues.push(`${label} remote WindBot host is not configured`);
            }
            if (!instance.botConfText?.trim()) {
                issues.push(instance.botConfUrl
                    ? `${label} bot.conf URL has not been fetched; refresh it first`
                    : `${label} remote mode requires bot.conf content or a URL`);
            }
        }
        return issues;
    };
    const currentIssues = [...baseIssues, ...inspectInstance('Current', settings.windbots.current)];
    const oldIssues = inspectInstance('Baseline', settings.windbots.old);
    const regressionIssues = [...currentIssues, ...oldIssues];
    if (sameWindBotEndpoint(settings.windbots.current, settings.windbots.old)) {
        regressionIssues.push('Current and baseline WindBot must not use the same service endpoint');
    }

    let decks = [];
    if (!regressionIssues.some((issue) => issue.includes('bot.conf'))) {
        try {
            decks = getRegressionCatalog(settings).map((item) => ({
                aiLevel: item.current.aiLevel,
                currentLabel: item.current.label,
                deck: item.deck,
                oldLabel: item.old.label,
            }));
            if (decks.length === 0) {
                regressionIssues.push('The two catalogs have no shared runnable decks');
            }
        } catch (error) {
            regressionIssues.push(error.message);
        }
    }
    let currentDecks = [];
    if (!currentIssues.some((issue) => issue.includes('bot.conf'))) {
        try {
            currentDecks = getCurrentCatalog(settings).map((item) => ({
                aiLevel: item.aiLevel,
                currentLabel: item.label,
                deck: item.deck,
            }));
            if (currentDecks.length === 0) {
                currentIssues.push('Current catalog has no runnable decks');
            }
        } catch (error) {
            currentIssues.push(error.message);
        }
    }
    let oldDecks = [];
    if (!oldIssues.some((issue) => issue.includes('bot.conf'))) {
        try {
            oldDecks = getOldCatalog(settings).map((item) => ({
                aiLevel: item.aiLevel,
                currentLabel: item.label,
                deck: item.deck,
            }));
            if (oldDecks.length === 0) {
                oldIssues.push('Baseline catalog has no runnable decks');
            }
        } catch (error) {
            oldIssues.push(error.message);
        }
    }
    const oldChallengeIssues = [...currentIssues, ...oldIssues];
    if (sameWindBotEndpoint(settings.windbots.current, settings.windbots.old)) {
        oldChallengeIssues.push('Current and baseline WindBot must not use the same service endpoint');
    }
    const currentMode = { issues: currentIssues, valid: currentIssues.length === 0 };
    return {
        currentDecks,
        decks,
        issues: regressionIssues,
        modes: {
            challenge: currentMode,
            challengeOld: { issues: oldChallengeIssues, valid: oldChallengeIssues.length === 0 },
            ranking: currentMode,
            regression: { issues: regressionIssues, valid: regressionIssues.length === 0 },
            tag: settings.windbots.current.mode === 'skynet'
                ? { valid: false, issues: [...currentIssues, 'SKYNET tag-duel support has not been validated'] }
                : currentMode,
        },
        oldDecks,
        valid: regressionIssues.length === 0,
    };
}

module.exports = {
    buildChallengeMatchups,
    buildRankingEntries,
    buildRegressionMatchups,
    buildTagEntries,
    getCurrentCatalog,
    getOldCatalog,
    getRegressionCatalog,
    inspectConfiguration,
    loadBotConfigText,
    loadBotsFromInstance,
    parseBotConfig,
};
