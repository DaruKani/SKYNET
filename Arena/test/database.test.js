'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const { ArenaDatabase } = require('../server/database');
const { normalizeRank } = require('../server/stats');

test('new workspace defaults are persisted without replacing saved agent paths', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-workspace-'));
    const databasePath = path.join(tempDir, 'arena.sqlite');
    let database = new ArenaDatabase(databasePath);
    context.after(() => {
        if (database.db.open) database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    const settings = database.getArenaSettings().settings;
    const windbotRoot = path.resolve(__dirname, '..', '..', 'WindBot');
    assert.equal(settings.windbots.current.runtimeDir, path.join(windbotRoot, 'bin', 'Release'));
    assert.equal(settings.windbots.current.botConfPath, path.join(windbotRoot, 'BotWrapper', 'bot.conf'));
    assert.equal(settings.windbots.current.port, 2399);
    assert.equal(settings.windbots.old.port, 2398);
    settings.windbots.current.runtimeDir = path.join(tempDir, 'custom-runtime');
    settings.windbots.current.botConfPath = path.join(tempDir, 'custom.conf');
    database.saveArenaSettings(settings);
    database.close();
    database = new ArenaDatabase(databasePath);
    assert.deepEqual(database.getArenaSettings().settings, settings);
});

test('scheduled stop persists in run configuration without changing other fields', (context) => {
    const database = new ArenaDatabase(':memory:');
    context.after(() => database.close());
    database.createRun({
        id: 'scheduled', kind: 'ranking', gamesPerMatchup: 0,
        createdAt: '2026-09-11T00:00:00.000Z', config: { selection: 'all' }, matchups: [], srvproId: 'srvpro-1',
    });
    database.setRunStopAt('scheduled', '2026-09-11T12:00:00.000Z');
    database.setRunStopAt('scheduled', '2026-09-11T13:00:00.000Z');
    const run = database.getRun('scheduled');
    assert.equal(run.config.stopAt, '2026-09-11T13:00:00.000Z');
    assert.equal(run.config.selection, 'all');
    assert.equal(run.srvproId, 'srvpro-1');
});

test('settling runs remain active and are interrupted on restart', (context) => {
    const database = new ArenaDatabase(':memory:');
    context.after(() => database.close());
    database.createRun({
        id: 'settling-run', kind: 'ranking', gamesPerMatchup: 0,
        createdAt: '2026-09-11T00:00:00.000Z', config: {}, matchups: [],
        srvproId: 'srvpro-1',
    });
    database.setRunStatus('settling-run', 'settling');
    assert.equal(database.markActiveRunsInterrupted(), 1);
    assert.equal(database.getRun('settling-run').status, 'interrupted');
});

test('run notes migrate existing records and persist across restarts', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-note-'));
    const databasePath = path.join(tempDir, 'arena.sqlite');
    let database = new ArenaDatabase(databasePath);
    context.after(() => {
        if (database.db.open) database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    database.createRun({
        id: 'legacy', kind: 'regression', gamesPerMatchup: 10,
        createdAt: '2026-08-08T00:00:00.000Z', config: {}, matchups: [],
        srvproId: 'srvpro-1',
    });
    database.db.exec('ALTER TABLE runs DROP COLUMN note');
    database.db.prepare('DELETE FROM schema_migrations WHERE version = ?').run('003_run_note.sql');
    database.close();
    database = new ArenaDatabase(databasePath);
    assert.equal(database.getRun('legacy').note, '');
    assert.equal(database.getRun('legacy').gamesPerMatchup, 10);
    assert.equal(database.setRunNote('legacy', 'Current version test <note>'), true);
    assert.equal(database.setRunNote('missing', 'Note'), false);
    database.close();
    database = new ArenaDatabase(databasePath);
    assert.equal(database.getRun('legacy').note, 'Current version test <note>');
    assert.equal(database.listRuns()[0].note, 'Current version test <note>');
    database.setRunNote('legacy', '');
    assert.equal(database.getRun('legacy').note, '');
});

test('ArenaDatabase migrates the single SRVPro setting without losing its secret', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-settings-migration-'));
    const databasePath = path.join(tempDir, 'arena.sqlite');
    let database = new ArenaDatabase(databasePath);
    context.after(() => {
        if (database.db.open) {
            database.close();
        }
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    const current = database.getArenaSettings().settings;
    const legacy = {
        srvpro: {
            ...current.srvpros[0],
            host: 'legacy-srvpro.lan',
            password: 'legacy-secret',
        },
        windbots: current.windbots,
    };
    database.db.prepare('UPDATE arena_settings SET settings_json = ? WHERE singleton = 1')
        .run(JSON.stringify(legacy));
    database.db.prepare('DELETE FROM schema_migrations WHERE version = ?')
        .run('002_multiple_srvpro_settings.sql');
    database.close();

    database = new ArenaDatabase(databasePath);
    const migrated = database.getArenaSettings().settings;
    assert.equal('srvpro' in migrated, false);
    assert.equal(migrated.srvpros[0].id, 'srvpro-1');
    assert.equal(migrated.srvpros[0].name, 'SRVPro 1');
    assert.equal(migrated.srvpros[0].host, 'legacy-srvpro.lan');
    assert.equal(migrated.srvpros[0].password, 'legacy-secret');
});

test('ArenaDatabase persists a run and applies rank statistics', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-db-'));
    const database = new ArenaDatabase(path.join(tempDir, 'arena.sqlite'));
    context.after(() => {
        database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });

    const settingsRecord = database.getArenaSettings();
    settingsRecord.settings.srvpros[0].host = 'srvpro.lan';
    settingsRecord.settings.srvpros[0].roomsPerSecond = 4;
    database.saveArenaSettings(settingsRecord.settings);
    assert.equal(database.getArenaSettings().settings.srvpros[0].host, 'srvpro.lan');
    assert.equal(database.getArenaSettings().settings.srvpros[0].roomsPerSecond, 4);

    database.createRun({
        config: { duelServer: '127.0.0.1:7911' },
        createdAt: '2026-08-08T00:00:00.000Z',
        gamesPerMatchup: 10,
        id: 'run-1',
        kind: 'regression',
        srvproId: 'srvpro-1',
        matchups: [{
            aiLevel: 4,
            competitors: [
                {
                    botLabel: 'Dragon',
                    deck: 'Dragon',
                    dialog: null,
                    endpointHost: '127.0.0.1',
                    endpointPort: 2399,
                    executionMode: 'local',
                    rankName: 'New-Dragon',
                    slot: 1,
                    source: 'current',
                },
                {
                    botLabel: 'Legacy Dragon',
                    deck: 'Dragon',
                    dialog: null,
                    endpointHost: 'windbot-old.lan',
                    endpointPort: 2398,
                    executionMode: 'remote',
                    rankName: 'Old-Dragon',
                    slot: 2,
                    source: 'old',
                },
            ],
            label: 'Dragon',
        }],
    });
    const rawRank = [
        ['New-Dragon', { flee: 1, lose: 2, win: 7 }],
        ['Old-Dragon', { flee: 0, lose: 7, win: 3 }],
    ];
    database.recordRank('run-1', normalizeRank(rawRank), rawRank);
    database.addEvent('run-1', 'info', 'legacy-wording', '\u5df2\u521b\u5efa 1 \u4e2a\u5bf9\u5c40\u7ec4');
    const run = database.getRun('run-1');

    assert.equal(run.matchups[0].observedGames, 9);
    assert.equal(run.matchups[0].aiLevel, 4);
    assert.equal(run.matchups[0].competitors[1].endpointHost, 'windbot-old.lan');
    assert.equal(run.matchups[0].currentWinRate, 0.7);
    assert.equal(run.status, 'preparing');
    assert.equal(run.startedAt, null);
    assert.equal(database.listRuns()[0].deckName, 'Dragon');
    assert.equal(database.listRuns()[0].matchupCount, 1);
    database.createRun({
        config: {},
        createdAt: '2026-08-08T01:00:00.000Z',
        gamesPerMatchup: 0,
        id: 'run-2',
        kind: 'ranking',
        srvproId: 'srvpro-2',
        matchups: [],
    });
    assert.equal(database.getRunCount(), 2);
    assert.deepEqual(
        database.findActiveRuns().map((run) => run.srvproId).sort(),
        ['srvpro-1', 'srvpro-2'],
    );
    assert.equal(database.listRuns(1, 0)[0].id, 'run-2');
    assert.equal(database.listRuns(1, 1)[0].id, 'run-1');
    const unmatchedRankAt = database.recordRank(null, normalizeRank(rawRank), rawRank);
    assert.equal(database.getLatestRankAt(), unmatchedRankAt);

    const plan = database.db.prepare(`
        EXPLAIN QUERY PLAN
        SELECT * FROM matchups WHERE run_id = ? ORDER BY ordinal
    `).all('run-1');
    assert.match(plan.map((row) => row.detail).join(' '), /idx_matchups_run_id/);

    assert.equal(database.deleteRun('run-1'), false);
    database.setRunStatus('run-1', 'completed', { finishedAt: '2026-08-08T00:10:00.000Z' });
    assert.equal(database.deleteRun('run-1'), true);
    assert.equal(database.getRun('run-1'), null);
    assert.equal(database.db.prepare('SELECT COUNT(*) AS count FROM matchups WHERE run_id = ?').get('run-1').count, 0);
    assert.equal(database.db.prepare('SELECT COUNT(*) AS count FROM run_events WHERE run_id = ?').get('run-1').count, 0);
    assert.equal(database.db.prepare('SELECT COUNT(*) AS count FROM rank_reports WHERE run_id = ?').get('run-1').count, 0);
});

test('ArenaDatabase derives challenge results from each opponent ranking', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-challenge-db-'));
    const database = new ArenaDatabase(path.join(tempDir, 'arena.sqlite'));
    context.after(() => {
        database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    const competitor = (deck, rankName, slot, source) => ({
        botLabel: rankName,
        deck,
        dialog: 'default',
        endpointHost: '127.0.0.1',
        endpointPort: 2399,
        executionMode: 'local',
        rankName,
        slot,
        source,
    });
    database.createRun({
        config: { targetDeck: 'Albaz' },
        createdAt: '2026-08-08T00:00:00.000Z',
        gamesPerMatchup: 100,
        id: 'challenge-1',
        kind: 'challenge',
        srvproId: 'srvpro-1',
        matchups: [
            {
                aiLevel: 2,
                competitors: [
                    competitor('Albaz', 'Albaz', 1, 'target'),
                    competitor('Dragon', 'Dragon Bot', 2, 'opponent'),
                ],
                label: 'Dragon',
            },
            {
                aiLevel: 4,
                competitors: [
                    competitor('Albaz', 'Albaz', 1, 'target'),
                    competitor('Spellbook', 'Spellbook Bot', 2, 'opponent'),
                ],
                label: 'Spellbook',
            },
        ],
    });
    const rawRank = [
        ['Albaz', { flee: 1, lose: 8, win: 11 }],
        ['Dragon Bot', { flee: 1, lose: 7, win: 3 }],
        ['Spellbook Bot', { flee: 0, lose: 4, win: 5 }],
    ];
    database.recordRank('challenge-1', normalizeRank(rawRank), rawRank);
    const run = database.getRun('challenge-1');

    assert.equal(run.challengeTargetFlee, 1);
    assert.deepEqual(run.matchups.map((matchup) => matchup.label), ['Dragon', 'Spellbook']);
    assert.ok(run.matchups.every((matchup) => matchup.targetGames === 100));
    assert.deepEqual(run.matchups.map((matchup) => matchup.competitors[1].flee), [1, 0]);
    const inferredTarget = run.matchups[0].competitors[0];
    assert.deepEqual(
        {
            flee: inferredTarget.flee,
            games: inferredTarget.games,
            lose: inferredTarget.lose,
            win: inferredTarget.win,
        },
        { flee: 0, games: 10, lose: 3, win: 7 },
    );
    assert.equal(run.matchups[0].currentWinRate, 0.7);
    assert.equal(run.matchups[1].currentWinRate, 4 / 9);
    assert.equal(run.observedGames, 19);
});

test('ArenaDatabase records an event when an active run is marked interrupted', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-interrupted-db-'));
    const database = new ArenaDatabase(path.join(tempDir, 'arena.sqlite'));
    context.after(() => {
        database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    database.createRun({
        config: {},
        createdAt: '2026-08-08T00:00:00.000Z',
        gamesPerMatchup: 0,
        id: 'interrupted-1',
        kind: 'ranking',
        srvproId: 'srvpro-1',
        matchups: [],
    });

    assert.equal(database.markActiveRunsInterrupted(), 1);
    assert.equal(database.markActiveRunsInterrupted(), 0);
    const run = database.getRun('interrupted-1');
    assert.equal(run.status, 'interrupted');
    assert.equal(run.stopReason, 'Skynet Arena restarted');
    assert.equal(run.events.length, 2);
    assert.equal(run.events.at(-1).level, 'warning');
    assert.equal(run.events.at(-1).message, 'Skynet Arena restarted; the run was interrupted');
    assert.equal(run.events.at(-1).type, 'interrupted');
});

test('ArenaDatabase represents unlimited ranking entries and counts launched pairs once', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-ranking-db-'));
    const database = new ArenaDatabase(path.join(tempDir, 'arena.sqlite'));
    context.after(() => {
        database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    const competitor = (deck, rankName) => ({
        botLabel: deck,
        deck,
        dialog: 'default',
        endpointHost: '127.0.0.1',
        endpointPort: 2399,
        executionMode: 'local',
        rankName,
        slot: 1,
        source: 'current',
    });
    const run = database.createRun({
        config: {},
        createdAt: '2026-08-08T00:00:00.000Z',
        gamesPerMatchup: 0,
        id: 'ranking-1',
        kind: 'ranking',
        srvproId: 'srvpro-1',
        matchups: [
            { aiLevel: 4, competitors: [competitor('Dragon', 'Rank001')], label: 'Dragon' },
            { aiLevel: 2, competitors: [competitor('Spellbook', 'Rank002')], label: 'Spellbook' },
        ],
    });
    database.recordLaunch('ranking-1', run.matchups.map((item) => item.id));
    const rawRank = [
        ['Rank001', { flee: 1, lose: 3, win: 6 }],
        ['Rank002', { flee: 0, lose: 6, win: 3 }],
    ];
    database.recordRank('ranking-1', normalizeRank(rawRank), rawRank);
    const updated = database.getRun('ranking-1');

    assert.equal(updated.totalGames, 0);
    assert.equal(updated.launchedGames, 1);
    assert.deepEqual(updated.matchups.map((item) => item.launchedGames), [1, 1]);
    assert.equal(updated.matchups[0].observedGames, 9);
    assert.equal(updated.matchups[0].currentWinRate, 2 / 3);
    assert.equal(updated.observedGames, 9);
});

test('ArenaDatabase counts all WindBot output errors for a tag run', (context) => {
    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'windbot-arena-tag-errors-db-'));
    const database = new ArenaDatabase(path.join(tempDir, 'arena.sqlite'));
    context.after(() => {
        database.close();
        fs.rmSync(tempDir, { force: true, recursive: true });
    });
    database.createRun({
        config: { windbots: { current: { mode: 'local' } } },
        createdAt: '2026-08-13T00:00:00.000Z',
        gamesPerMatchup: 0,
        id: 'tag-errors-1',
        kind: 'tag',
        srvproId: 'srvpro-1',
        matchups: [{
            aiLevel: 4,
            competitors: [{
                botLabel: 'Alpha',
                deck: 'Alpha',
                dialog: 'default',
                endpointHost: '127.0.0.1',
                endpointPort: 2399,
                executionMode: 'local',
                rankName: 'Alpha',
                slot: 1,
                source: 'current',
            }],
            label: 'Alpha',
        }],
    });
    database.addEvent('tag-errors-1', 'error', 'windbot-output-error', 'first');
    database.addEvent('tag-errors-1', 'warning', 'schedule-error', 'not WindBot output');
    database.addEvent('tag-errors-1', 'error', 'windbot-output-error', 'second');

    assert.equal(database.getRun('tag-errors-1').windbotOutputErrorCount, 2);
});
