'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const {
    buildChallengeMatchups,
    buildRankingEntries,
    buildRegressionMatchups,
    buildTagEntries,
    getRegressionCatalog,
    inspectConfiguration,
    parseBotConfig,
} = require('../server/bot-config');
const { createDefaultArenaSettings } = require('../server/arena-settings');

const botConfig = `
!Dragon Bot
Name=Dragon Deck=Dragon Dialog=dragon
AI_LV4
!Quoted Bot
Name=Quoted Deck='Deck With Spaces' Dialog=quoted
AI_LV2
!Beginner Bot
AI_LV1
Name=Beginner Deck=Beginner Dialog=beginner
!Unleveled Bot
Name=Unleveled Deck=Unleveled Dialog=default
!Manual Bot
Name=Manual Deck=SELECT_DECKFILE Dialog=manual
`;

test('configuration inspection reports incomplete settings without throwing', () => {
    const settings = createDefaultArenaSettings();
    const result = inspectConfiguration(settings);

    assert.equal(result.valid, false);
    assert.equal(result.modes.ranking.valid, false);
    assert.ok(result.issues.includes('SRVPro host is not configured'));
    assert.ok(result.issues.includes('Current WindBot runtime directory is not configured'));
    assert.ok(result.issues.includes('Current bot.conf path is not configured'));
});

test('parseBotConfig parses supported entries and filters manual bots', () => {
    const bots = parseBotConfig(botConfig);
    assert.deepEqual([...bots.keys()], ['Dragon', 'Deck With Spaces', 'Beginner', 'Unleveled']);
    assert.deepEqual(bots.get('Dragon'), {
        aiLevel: 4,
        deck: 'Dragon',
        dialog: 'dragon',
        label: 'Dragon Bot',
    });
    assert.equal(bots.get('Beginner').aiLevel, 1);
    assert.equal(bots.get('Unleveled').aiLevel, null);
});

test('parseBotConfig accepts MDPro4 quoted names and English dialogs', () => {
    const bots = parseBotConfig(`
!Mokey Mokey King
Name='Mokey Mokey King' Deck='MokeyMokeyKing' Dialog=Mokey.EN
AI_LV1 SUPPORT_MASTER_RULE_2020
!LV8
Name='Level VIII' Deck='Level VIII' Dialog=Swordsman.EN
AI_LV2 SUPPORT_MASTER_RULE_2020
!Select a deck
Name='Selected deck' Deck=SELECT_DECKFILE Dialog=Universal_4.EN
SELECT_DECKFILE
`);
    assert.deepEqual([...bots.keys()], ['MokeyMokeyKing', 'Level VIII']);
    assert.equal(bots.get('MokeyMokeyKing').dialog, 'Mokey.EN');
    assert.equal(bots.get('Level VIII').aiLevel, 2);
});

test('buildRegressionMatchups creates generalized competitors', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-bots-'));
    context.after(() => fs.rmSync(tempDir, { force: true, recursive: true }));
    const currentDir = path.join(tempDir, 'current');
    const oldDir = path.join(tempDir, 'old');
    for (const runtimeDir of [currentDir, oldDir]) {
        fs.mkdirSync(path.join(runtimeDir, 'Dialogs'), { recursive: true });
        fs.writeFileSync(path.join(runtimeDir, 'WindBot.exe'), 'fixture');
    }
    fs.writeFileSync(path.join(currentDir, 'Dialogs', 'dragon.json'), '{}');
    const currentBotConf = path.join(tempDir, 'current.conf');
    const oldBotConf = path.join(tempDir, 'old.conf');
    fs.writeFileSync(currentBotConf, botConfig);
    fs.writeFileSync(oldBotConf, botConfig.replace('Dragon Bot', 'Legacy Dragon'));

    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfPath: currentBotConf,
        runtimeDir: currentDir,
    };
    settings.windbots.old = {
        ...settings.windbots.old,
        botConfPath: oldBotConf,
        runtimeDir: oldDir,
    };
    const matchups = buildRegressionMatchups(settings, ['Dragon']);

    assert.equal(matchups.length, 1);
    assert.equal(matchups[0].competitors[0].rankName, 'C-Dragon Bot');
    assert.equal(matchups[0].competitors[1].rankName, 'B-Legacy Dragon');
    assert.equal(matchups[0].aiLevel, 4);
    assert.equal(matchups[0].competitors[0].dialog, 'dragon');
    assert.equal(matchups[0].competitors[1].dialog, null);

    const [challengeMatchup] = buildChallengeMatchups(
        settings,
        'Dragon — Legacy Dragon',
        ['Deck With Spaces'],
        'old',
    );
    assert.equal(challengeMatchup.competitors[0].endpointPort, 2398);
    assert.equal(challengeMatchup.competitors[0].executionMode, 'local');
    assert.equal(challengeMatchup.competitors[0].dialog, null);
    assert.equal(challengeMatchup.competitors[1].endpointPort, 2399);
    assert.equal(challengeMatchup.competitors[1].executionMode, 'local');
});

test('remote WindBot uses pasted bot.conf and remote endpoint', () => {
    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    settings.windbots.old = {
        ...settings.windbots.old,
        botConfText: botConfig.replace('Dragon Bot', 'Legacy Dragon'),
        host: 'windbot-old.lan',
        mode: 'remote',
    };
    const [matchup] = buildRegressionMatchups(settings, ['Dragon']);
    assert.equal(matchup.competitors[0].endpointHost, 'windbot-current.lan');
    assert.equal(matchup.competitors[0].executionMode, 'remote');
    assert.equal(matchup.competitors[0].dialog, 'dragon');
});

test('catalog sorts AI levels descending while bulk selection excludes AI_LV1', () => {
    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    settings.windbots.old = {
        ...settings.windbots.old,
        botConfText: botConfig,
        host: 'windbot-old.lan',
        mode: 'remote',
    };
    const catalog = getRegressionCatalog(settings);
    assert.deepEqual(catalog.map((item) => item.deck), [
        'Dragon',
        'Deck With Spaces',
        'Beginner',
        'Unleveled',
    ]);
    const matchups = buildRegressionMatchups(settings, []);
    assert.deepEqual(matchups.map((item) => item.label), [
        'Dragon',
        'Deck With Spaces',
        'Unleveled',
    ]);
    assert.equal(buildRegressionMatchups(settings, ['Beginner'])[0].label, 'Beginner');
});

test('challenge shares the target rank and keeps opponent bot names', () => {
    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    settings.windbots.old = {
        ...settings.windbots.old,
        botConfText: botConfig.replace('Dragon Bot', 'Legacy Dragon'),
        host: 'windbot-old.lan',
        mode: 'remote',
    };
    const matchups = buildChallengeMatchups(settings, 'ManualExecutor', []);

    assert.deepEqual(matchups.map((item) => item.label), [
        'Dragon',
        'Deck With Spaces',
        'Unleveled',
        'ManualExecutor',
    ]);
    assert.equal(matchups[0].competitors[0].deck, 'ManualExecutor');
    assert.equal(matchups[0].competitors[0].dialog, 'default');
    assert.equal(matchups.at(-1).competitors[1].deck, 'ManualExecutor');
    assert.equal(matchups[0].competitors[0].endpointHost, 'windbot-current.lan');
    assert.equal(matchups[0].competitors[0].rankName, 'ManualExecutor');
    assert.equal(matchups[0].competitors[1].rankName, 'Dragon Bot');
    assert.ok(matchups.every((item) => item.competitors[0].rankName === 'ManualExecutor'));
    assert.equal(matchups.at(-1).competitors[1].rankName, 'Opp-ManualExecutor');
    assert.equal(new Set(matchups.flatMap((item) => item.competitors.map((bot) => bot.rankName))).size, 5);

    const [oldTargetMatchup] = buildChallengeMatchups(
        settings,
        'Dragon — Legacy Dragon',
        ['Deck With Spaces'],
        'old',
    );
    assert.equal(oldTargetMatchup.competitors[0].deck, 'Dragon');
    assert.equal(oldTargetMatchup.competitors[0].endpointHost, 'windbot-old.lan');
    assert.equal(oldTargetMatchup.competitors[0].botLabel, 'Legacy Dragon');
    assert.equal(oldTargetMatchup.competitors[1].endpointHost, 'windbot-current.lan');
    assert.equal(oldTargetMatchup.competitors[1].botLabel, 'Quoted Bot');

    const beginnerMatchups = buildChallengeMatchups(settings, 'Beginner', []);
    assert.ok(beginnerMatchups.some((item) => item.label === 'Beginner'));

    const aliasSettings = structuredClone(settings);
    aliasSettings.windbots.current.botConfText += `
!Écclesia-Albaz
Name=Écclesia Deck=Albaz Dialog=ecclesia.zh-CN
AI_LV4
`;
    const [albazMatchup] = buildChallengeMatchups(
        aliasSettings,
        'Albaz — Écclesia-Albaz',
        ['Dragon'],
    );
    assert.equal(albazMatchup.competitors[0].deck, 'Albaz');
    assert.equal(albazMatchup.competitors[0].rankName, 'Albaz');

    const [longNameMatchup] = buildChallengeMatchups(
        settings, 'ManualExecutorWithAnExtremelyLongName', ['Dragon'],
    );
    assert.equal(longNameMatchup.competitors[0].deck, 'ManualExecutorWithAnExtremelyLongName');
    assert.match(longNameMatchup.competitors[0].rankName, /^ManualExec-[0-9a-f]{8}$/);
});

test('long MDPro4 labels retain their decks and display text with stable bounded participant names', () => {
    const labels = [
        'Ritual of Light and Darkness',
        'Rush Dragon Dragears',
        'Dinocarriage Dynarmix',
        'Ritual of Light and Dragons',
        '😀😀😀😀😀😀😀😀😀😀',
    ];
    const settings = createDefaultArenaSettings();
    for (const source of ['current', 'old']) {
        Object.assign(settings.windbots[source], {
            mode: 'remote', host: `${source}.example`,
            botConfText: labels.map((label, index) => (
                `!${label}\nName='${label}' Deck=Deck${index} Dialog=Universal.EN\nAI_LV2`
            )).join('\n'),
        });
    }
    const matchups = buildRegressionMatchups(settings);
    const names = matchups.flatMap((matchup) => matchup.competitors.map((bot) => bot.rankName));
    assert.equal(new Set(names).size, labels.length * 2);
    assert.ok(names.every((name) => name.length <= 19 && name.isWellFormed()));
    for (const matchup of matchups) {
        const index = Number(matchup.label.slice(4));
        assert.ok(matchup.competitors.every((bot) => bot.botLabel === labels[index]));
        assert.ok(matchup.competitors.every((bot) => bot.deck === `Deck${index}`));
    }
    assert.deepEqual(buildRegressionMatchups(settings), matchups);
    const challenge = buildChallengeMatchups(settings, 'Deck1', ['Deck0', 'Deck3']);
    assert.equal(challenge[0].competitors[0].rankName, 'Deck1');
    assert.equal(challenge[0].competitors[1].botLabel, labels[0]);
    assert.notEqual(challenge[0].competitors[1].rankName, challenge[1].competitors[1].rankName);
    assert.ok(challenge.every((matchup) => matchup.competitors[1].rankName.length <= 19));
});

test('ranking creates one current WindBot entry per selected deck', () => {
    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    const entries = buildRankingEntries(settings, ['Dragon', 'Beginner']);
    assert.equal(entries.length, 2);
    assert.equal(entries[0].competitors.length, 1);
    assert.equal(entries[0].competitors[0].source, 'current');
    assert.equal(entries[0].competitors[0].rankName, 'Dragon Bot');
    assert.equal(entries[1].competitors[0].rankName, 'Beginner Bot');
    assert.notEqual(entries[0].competitors[0].rankName, entries[1].competitors[0].rankName);
    assert.throws(() => buildRankingEntries(settings, ['Dragon']), /requires at least two decks/);
});

test('tag smoke test creates one current WindBot entry for each candidate deck', () => {
    const settings = createDefaultArenaSettings();
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    const entries = buildTagEntries(settings, [
        'Dragon',
        'Deck With Spaces',
        'Beginner',
        'Unleveled',
    ]);

    assert.deepEqual(entries.map((entry) => entry.label), [
        'Dragon',
        'Deck With Spaces',
        'Beginner',
        'Unleveled',
    ]);
    assert.ok(entries.every((entry) => entry.competitors.length === 1));
    assert.ok(entries.every((entry) => entry.competitors[0].source === 'current'));
    assert.equal(buildTagEntries(settings, ['Dragon']).length, 1);
});

test('current-only modes remain available when the old WindBot is not configured', () => {
    const settings = createDefaultArenaSettings();
    settings.srvpros[0] = {
        ...settings.srvpros[0],
        host: 'srvpro.lan',
        password: 'password',
        username: 'admin',
    };
    settings.windbots.current = {
        ...settings.windbots.current,
        botConfText: botConfig,
        host: 'windbot-current.lan',
        mode: 'remote',
    };
    const inspection = inspectConfiguration(settings);
    assert.equal(inspection.modes.challenge.valid, true);
    assert.equal(inspection.modes.challengeOld.valid, false);
    assert.equal(inspection.modes.ranking.valid, true);
    assert.equal(inspection.modes.tag.valid, true);
    assert.equal(inspection.modes.regression.valid, false);
    assert.equal(inspection.currentDecks.length, 4);
    assert.equal(inspection.oldDecks.length, 0);
});
