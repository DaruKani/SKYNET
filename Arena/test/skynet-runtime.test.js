'use strict';

const assert = require('node:assert/strict');
const childProcess = require('node:child_process');
const { EventEmitter } = require('node:events');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { PassThrough } = require('node:stream');
const test = require('node:test');
const { createDefaultArenaSettings, validateAndMergeArenaSettings } = require('../server/arena-settings');
const { buildRegressionMatchups, buildChallengeMatchups, buildTagEntries, inspectConfiguration } = require('../server/bot-config');
const { SkynetRuntime, buildSkynetCommand } = require('../server/skynet-runtime');
const { ArenaDatabase } = require('../server/database');
const { ArenaService } = require('../server/arena-service');

function settingsFixture(t) {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'skynet-arena-test-'));
    t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
    const executablePath = path.join(directory, 'SKYNET.exe');
    const databasePath = path.join(directory, 'cards.cdb');
    const deckPath = path.join(directory, 'starter ; harmless.ydk');
    for (const filename of [executablePath, databasePath, deckPath]) fs.writeFileSync(filename, 'fixture');
    const settings = createDefaultArenaSettings();
    Object.assign(settings.srvpros[0], { host: '127.0.0.1', username: 'admin', password: 'secret' });
    for (const [name, instance] of Object.entries(settings.windbots)) {
        instance.mode = 'skynet';
        Object.assign(instance.skynet, {
            executablePath,
            databasePath,
            maxProcesses: 2,
            profiles: [{
                id: 'starter', label: 'Starter', deckPath,
                agent: name === 'current' ? 'skynet' : 'expert-oldschool', modelPath: '',
            }],
        });
    }
    return { directory, settings, deckPath };
}

function contextFixture(id = 'run') {
    return {
        id, abortController: new AbortController(),
        settings: { srvpro: { host: '127.0.0.1', duelPort: 7911 } },
    };
}

function mockChildren(t, onSpawn) {
    const children = [];
    t.mock.method(childProcess, 'spawn', (exe, args, options) => {
        const child = new EventEmitter();
        Object.assign(child, {
            stdout: new PassThrough(), stderr: new PassThrough(),
            exitCode: null, signalCode: null, calls: [],
            kill(signal = 'SIGTERM') {
                this.calls.push(signal);
                queueMicrotask(() => this.finish(null, signal));
                return true;
            },
            finish(code = 0, signal = null) {
                if (this.exitCode !== null || this.signalCode !== null) return;
                this.exitCode = code; this.signalCode = signal;
                this.stdout.end(); this.stderr.end();
                this.emit('close', code, signal);
            },
        });
        children.push(child);
        onSpawn?.(child, exe, args, options);
        queueMicrotask(() => child.stdout.write('SKYNET_SEAT seat=0\n'));
        return child;
    });
    return children;
}

test('SKYNET settings preserve WindBot modes and compare explicit profiles on the same executable', (t) => {
    const { settings, deckPath } = settingsFixture(t);
    const merged = validateAndMergeArenaSettings(settings, settings);
    const matches = buildRegressionMatchups(merged, ['starter']);
    assert.equal(matches.length, 1);
    assert.equal(matches[0].competitors[0].skynet.profile.agent, 'skynet');
    assert.equal(matches[0].competitors[1].skynet.profile.agent, 'expert-oldschool');
    assert.equal(matches[0].competitors[0].skynet.profile.deckPath, deckPath);
    assert.equal(inspectConfiguration(merged).modes.regression.valid, true);
    assert.equal(inspectConfiguration(merged).modes.tag.valid, false);
    assert.equal(buildChallengeMatchups(merged, 'starter', ['starter'])[0].competitors[0].rankName, 'T-Starter');
    assert.throws(() => buildTagEntries(merged), /not been validated/);
    assert.throws(() => buildChallengeMatchups(merged, 'unconfigured', ['starter']), /not configured/);
});

test('SKYNET rejects relative paths, invalid agents, duplicate profiles and expert learning models', (t) => {
    const { settings } = settingsFixture(t);
    const configuration = settings.windbots.current.skynet;
    const original = structuredClone(configuration);
    for (const change of [
        () => { configuration.databasePath = '../cards.cdb'; },
        () => { configuration.profiles[0].agent = 'unknown-executor'; },
        () => { configuration.profiles.push(configuration.profiles[0]); },
        () => { configuration.maxProcesses = 1; },
        () => { configuration.profiles[0].agent = 'expert-oldschool'; configuration.profiles[0].modelPath = path.join(os.tmpdir(), 'model.json'); },
    ]) {
        Object.assign(configuration, structuredClone(original));
        change();
        assert.throws(() => validateAndMergeArenaSettings(settings, settings));
    }
});

test('SKYNET launch uses fixed argument keys, absolute resources and disabled training without a shell', (t) => {
    const { settings, deckPath } = settingsFixture(t);
    const competitor = buildRegressionMatchups(settings)[0].competitors[0];
    const command = buildSkynetCommand(competitor, settings.srvpros[0], 'M#123456789');
    assert.equal(command.executable, settings.windbots.current.skynet.executablePath);
    assert.equal(command.options.shell, false);
    assert.equal(command.options.windowsHide, true);
    assert.ok(command.args.includes(`DeckFile=${deckPath}`));
    assert.ok(command.args.includes('PolicyTraining=False'));
    assert.ok(command.args.includes('PolicyModel='));
    assert.ok(command.args.includes('HostInfo=M#123456789'));
    assert.equal(command.args.filter((value) => value.startsWith('DeckFile=')).length, 1);
});

test('room reservation prevents partial-pair deadlock and releases slots only after owned children close', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const first = contextFixture('first');
    const second = contextFixture('second');
    const children = mockChildren(t);
    await runtime.reserveRoom(first, players, 'M#100000001');
    await runtime.launch(first, players[0], 'M#100000001');
    await runtime.launch(first, players[1], 'M#100000001');
    let admitted = false;
    const waiting = runtime.reserveRoom(second, players, 'M#100000002').then(() => { admitted = true; });
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(admitted, false);
    assert.equal(children.length, 2);
    for (const child of children) {
        child.stdout.write('SKYNET_RESULT result=2\nSKYNET_END\n');
        child.finish();
    }
    await waiting;
    assert.equal(admitted, true);
    assert.equal(runtime.activeCount(first), 0);
    assert.equal(runtime.activeCount(second), 2);
    assert.equal(first.abortController.signal.aborted, false);
    await runtime.stopContext(second);
    assert.equal(runtime.activeCount(), 0);
});

test('abort retires only children owned by that run and does not report a loss', async (t) => {
    const { settings } = settingsFixture(t);
    settings.windbots.current.skynet.maxProcesses = 4;
    settings.windbots.old.skynet.maxProcesses = 4;
    const players = buildRegressionMatchups(settings)[0].competitors;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const first = contextFixture('first');
    const second = contextFixture('second');
    const children = mockChildren(t);
    for (const [context, password] of [[first, 'M#100000001'], [second, 'M#100000002']]) {
        await runtime.reserveRoom(context, players, password);
        await runtime.launch(context, players[0], password);
        await runtime.launch(context, players[1], password);
    }
    first.abortController.abort(new DOMException('User stop', 'AbortError'));
    assert.equal(await runtime.stopContext(first), true);
    assert.equal(runtime.activeCount(first), 0);
    assert.equal(runtime.activeCount(second), 2);
    assert.equal(children[0].calls.length, 1);
    assert.equal(children[1].calls.length, 1);
    assert.equal(children[2].calls.length, 0);
    assert.equal(children[3].calls.length, 0);
    assert.equal(second.abortController.signal.aborted, false);
});

test('an unexpected client exit interrupts its run rather than creating a terminal game result', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const context = contextFixture();
    const children = mockChildren(t);
    await runtime.reserveRoom(context, players, 'M#100000001');
    await runtime.launch(context, players[0], 'M#100000001');
    children[0].finish(1);
    assert.equal(context.abortController.signal.aborted, true);
    assert.match(context.abortController.signal.reason.message, /without clean completion/);
    await runtime.stopContext(context);
    assert.equal(runtime.activeCount(), 0);
});

test('waiting for process capacity is cancellable and reserves no partial children', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const occupied = contextFixture('occupied');
    const waiting = contextFixture('waiting');
    await runtime.reserveRoom(occupied, players, 'M#100000001');
    const task = runtime.reserveRoom(waiting, players, 'M#100000002');
    waiting.abortController.abort(new DOMException('Cancelled', 'AbortError'));
    await assert.rejects(task, /Cancelled/);
    assert.equal(runtime.activeCount(occupied), 2);
    assert.equal(runtime.activeCount(waiting), 0);
});

test('absolute room deadline aborts the run and retires every owned child', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    // The settings validator enforces a 10-second minimum; shorten only this
    // isolated manager fixture to exercise the actual deadline handler.
    for (const player of players) player.skynet.duelTimeoutSeconds = .03;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const context = contextFixture();
    const children = mockChildren(t);
    await runtime.reserveRoom(context, players, 'M#100000001');
    await runtime.launch(context, players[0], 'M#100000001');
    await runtime.launch(context, players[1], 'M#100000001');
    await new Promise((resolve) => setTimeout(resolve, 60));
    assert.equal(context.abortController.signal.aborted, true);
    assert.match(context.abortController.signal.reason.message, /bounded match deadline/);
    assert.equal(runtime.activeCount(), 0);
    assert.equal(children.every((child) => child.calls.length === 1), true);
});

test('Arena routes SKYNET participants to the owned process adapter and retains remote HTTP mode', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    const service = new ArenaService({}, {});
    t.after(() => service.skynetRuntime.shutdown());
    const context = contextFixture();
    context.settings.srvpro.roomsPerSecond = 100;
    context.nextRoomNumber = 123456789;
    const children = mockChildren(t);
    let httpRequests = 0;
    t.mock.method(global, 'fetch', async (url) => {
        httpRequests++;
        assert.equal(url.hostname, 'remote.example.test');
        assert.equal(url.searchParams.get('password'), 'M#123456789');
        assert.equal(url.searchParams.get('deck'), 'LegacyDeck');
        return new Response('', { status: 200 });
    });
    const remote = {
        executionMode: 'remote', endpointHost: 'remote.example.test', endpointPort: 2398,
        rankName: 'Baseline', deck: 'LegacyDeck', dialog: 'default',
    };
    await service.launchRoom(context, [players[0], remote]);
    assert.equal(children.length, 1);
    assert.equal(httpRequests, 1);
    assert.equal(service.skynetRuntime.activeCount(context), 1);
    context.abortController.abort(new DOMException('Test finished', 'AbortError'));
    assert.equal(await service.skynetRuntime.stopContext(context), true);
    assert.equal(service.skynetRuntime.activeCount(), 0);
});

test('launch failure retires the sibling reservation and does not invoke an HTTP bot endpoint', async (t) => {
    const { settings } = settingsFixture(t);
    const players = buildRegressionMatchups(settings)[0].competitors;
    const runtime = new SkynetRuntime();
    t.after(() => runtime.shutdown());
    const context = contextFixture();
    t.mock.method(childProcess, 'spawn', () => { throw new Error('spawn denied'); });
    await runtime.reserveRoom(context, players, 'M#100000001');
    await assert.rejects(runtime.launch(context, players[0], 'M#100000001'), /spawn denied/);
    await runtime.stopRoom(context, 'M#100000001');
    assert.equal(runtime.activeCount(), 0);
});

test('existing SQLite settings migrate additively and SKYNET settings persist', (t) => {
    const { settings, directory } = settingsFixture(t);
    const filename = path.join(directory, 'arena.sqlite');
    const db = new ArenaDatabase(filename);
    const legacy = createDefaultArenaSettings();
    delete legacy.windbots.current.skynet;
    delete legacy.windbots.old.skynet;
    db.saveArenaSettings(legacy);
    db.db.prepare('DELETE FROM schema_migrations WHERE version = ?').run('005_skynet_runtime.sql');
    db.close();
    const migrated = new ArenaDatabase(filename);
    try {
        assert.equal(migrated.getArenaSettings().settings.windbots.current.mode, 'local');
        assert.equal(migrated.getArenaSettings().settings.windbots.current.skynet.maxProcesses, 4);
        migrated.saveArenaSettings(settings);
        assert.equal(migrated.getArenaSettings().settings.windbots.old.skynet.profiles[0].agent, 'expert-oldschool');
        assert.equal(migrated.db.prepare('SELECT COUNT(*) AS n FROM schema_migrations WHERE version = ?')
            .get('005_skynet_runtime.sql').n, 1);
    } finally {
        migrated.close();
    }
});
