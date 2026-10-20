'use strict';

const childProcess = require('node:child_process');
const path = require('node:path');

const GLOBAL_PROCESS_LIMIT = 32;
const MAX_LINE_LENGTH = 8192;

function abortError(message) {
    return new DOMException(message, 'AbortError');
}

function delay(milliseconds, signal) {
    return new Promise((resolve, reject) => {
        if (signal.aborted) return reject(signal.reason);
        const onAbort = () => { clearTimeout(timer); reject(signal.reason); };
        const timer = setTimeout(() => {
            signal.removeEventListener('abort', onAbort);
            resolve();
        }, milliseconds);
        signal.addEventListener('abort', onAbort, { once: true });
    });
}

function poolKey(config) {
    const filename = path.resolve(config.executablePath);
    return process.platform === 'win32' ? filename.toLowerCase() : filename;
}

function buildSkynetCommand(competitor, srvpro, password) {
    const config = competitor.skynet;
    const profile = config.profile;
    const args = [
        `Name=${competitor.rankName}`,
        `Agent=${profile.agent}`,
        'Deck=SKYNET',
        `DeckFile=${profile.deckPath}`,
        `DbPath=${config.databasePath}`,
        `Host=${srvpro.host}`,
        `Port=${srvpro.duelPort}`,
        `HostInfo=${password}`,
        `Version=${config.version}`,
        'Chat=False',
        'Debug=False',
        'PolicyTraining=False',
        // Also override application configuration; an empty profile is frozen baseline inference.
        `PolicyModel=${profile.modelPath || ''}`,
    ];
    return {
        executable: config.executablePath,
        args,
        options: {
            cwd: path.dirname(config.executablePath),
            shell: false,
            windowsHide: true,
            stdio: ['ignore', 'pipe', 'pipe'],
        },
    };
}

class SkynetRuntime {
    constructor(onOutput = () => {}) {
        this.onOutput = onOutput;
        this.rooms = new Map();
    }

    records() {
        return [...this.rooms.values()].flatMap((room) => room.records);
    }

    activeCount(context) {
        return this.records().filter((record) => !context || record.room.context === context).length;
    }

    // Reserve an entire room atomically. Reserving one process at a time can
    // deadlock two concurrent experiments, each holding a player awaiting a rival.
    async reserveRoom(context, players, password) {
        const localPlayers = players.filter((player) => player.executionMode === 'skynet');
        if (localPlayers.length === 0) return;
        const signal = context.abortController.signal;
        const deadline = Date.now() + Math.min(...localPlayers.map(
            (player) => player.skynet.duelTimeoutSeconds * 1000,
        ));
        const requested = new Map();
        for (const player of localPlayers) {
            const key = poolKey(player.skynet);
            const item = requested.get(key) || { count: 0, limit: player.skynet.maxProcesses };
            item.count++;
            item.limit = Math.min(item.limit, player.skynet.maxProcesses);
            requested.set(key, item);
        }
        for (const item of requested.values()) {
            if (item.count > item.limit) throw new Error('SKYNET process limit cannot accommodate this room');
        }
        for (;;) {
            if (signal.aborted) throw signal.reason;
            if (Date.now() >= deadline) throw new Error('SKYNET timed out waiting for local process capacity');
            const records = this.records();
            const available = records.length + localPlayers.length <= GLOBAL_PROCESS_LIMIT
                && [...requested].every(([key, item]) => {
                    const existing = records.filter((record) => record.key === key);
                    const limit = Math.min(item.limit, ...existing.map((record) => record.competitor.skynet.maxProcesses));
                    return existing.length + item.count <= limit;
                });
            if (available) break;
            await delay(Math.min(100, deadline - Date.now()), signal);
        }
        const key = `${context.id}:${password}`;
        if (this.rooms.has(key)) throw new Error('SKYNET room is already reserved');
        const room = { key, context, password, deadline, records: [], stopping: false };
        room.records = localPlayers.map((competitor) => ({
            competitor, key: poolKey(competitor.skynet), room, child: null, stopping: false,
        }));
        room.onAbort = () => { void this.stopRoom(context, password); };
        signal.addEventListener('abort', room.onAbort, { once: true });
        this.rooms.set(key, room);
        room.timer = setTimeout(() => {
            this.fail(room, new Error(`SKYNET room ${password} exceeded its bounded duel deadline`));
        }, Math.max(1, deadline - Date.now()));
        room.timer.unref();
    }

    fail(room, error) {
        if (!room.context.abortController.signal.aborted && !room.stopping) {
            room.context.abortController.abort(error);
        }
        void this.stopRoom(room.context, room.password);
    }

    release(record) {
        const room = record.room;
        room.records = room.records.filter((item) => item !== record);
        if (room.records.length === 0) {
            clearTimeout(room.timer);
            room.context.abortController.signal.removeEventListener('abort', room.onAbort);
            this.rooms.delete(room.key);
        }
    }

    async launch(context, competitor, password) {
        const room = this.rooms.get(`${context.id}:${password}`);
        const record = room?.records.find((item) => item.competitor === competitor && !item.child);
        if (!record || room.stopping || context.abortController.signal.aborted) {
            throw context.abortController.signal.reason || new Error('SKYNET room has no live process reservation');
        }
        const command = buildSkynetCommand(competitor, context.settings.srvpro, password);
        return new Promise((resolve, reject) => {
            let settled = false;
            let line = '';
            let receivedOutcome = false;
            let receivedEnd = false;
            const settle = (error) => {
                if (settled) return;
                settled = true;
                clearTimeout(record.joinTimer);
                if (error) reject(error); else resolve();
            };
            record.rejectLaunch = (error) => settle(error);
            let child;
            try {
                child = childProcess.spawn(command.executable, command.args, command.options);
            } catch (error) {
                this.release(record);
                settle(error);
                return;
            }
            record.child = child;
            record.closed = new Promise((closed) => { record.resolveClosed = closed; });
            this.onOutput(context, competitor, `[SKYNET] Starting ${competitor.rankName} in ${password}\n`);
            const inspectLine = (text) => {
                if (/\bSKYNET_SEAT\s+seat=\d+\b/.test(text)) settle();
                if (/\bSKYNET_RESULT\s+result=[012]\b/.test(text)) receivedOutcome = true;
                if (/\bSKYNET_END\b/.test(text)) receivedEnd = true;
            };
            child.stdout.on('data', (buffer) => {
                const text = buffer.toString('utf8');
                this.onOutput(context, competitor, text);
                // Scan bounded lines without retaining arbitrary child output.
                for (const fragment of text.split(/(\n)/)) {
                    if (fragment === '\n') { inspectLine(line); line = ''; }
                    else line = (line + fragment).slice(-MAX_LINE_LENGTH);
                }
            });
            child.stderr.on('data', (buffer) => {
                this.onOutput(context, competitor, `[stderr] ${buffer.toString('utf8')}`);
            });
            child.on('error', (error) => {
                settle(error);
                if (!record.stopping) this.fail(room, error);
            });
            child.on('close', (code, signal) => {
                inspectLine(line);
                clearTimeout(record.forceTimer);
                clearTimeout(record.joinTimer);
                this.release(record);
                record.resolveClosed();
                this.onOutput(context, competitor, `[SKYNET] ${competitor.rankName} exited: ${code ?? signal}\n`);
                const error = new Error(`SKYNET ${competitor.rankName} exited without a completed duel (code=${code}, signal=${signal || 'none'})`);
                settle(error);
                // Console markers diagnose completion; SRVPro scores remain the
                // sole result source. Aborts/timeouts must never become losses.
                if (!record.stopping && (code !== 0 || !receivedOutcome || !receivedEnd)) {
                    this.fail(room, error);
                }
            });
            record.joinTimer = setTimeout(() => {
                const error = new Error(`SKYNET ${competitor.rankName} did not join its room before the deadline`);
                settle(error);
                this.fail(room, error);
            }, Math.max(1, Math.min(10000, room.deadline - Date.now())));
            record.joinTimer.unref();
        });
    }

    async stopRoom(context, password) {
        const room = this.rooms.get(`${context.id}:${password}`);
        if (!room) return true;
        room.stopping = true;
        clearTimeout(room.timer);
        const records = [...room.records];
        for (const record of records) {
            if (record.stopping) continue;
            record.stopping = true;
            record.rejectLaunch?.(context.abortController.signal.reason || abortError('SKYNET room retired'));
            clearTimeout(record.joinTimer);
            if (!record.child) {
                this.release(record);
                continue;
            }
            if (record.child.exitCode === null && record.child.signalCode === null) {
                record.child.kill();
                record.forceTimer = setTimeout(() => {
                    if (record.child.exitCode === null && record.child.signalCode === null) record.child.kill('SIGKILL');
                }, 1000);
                record.forceTimer.unref();
            }
        }
        let timer;
        const closed = await Promise.race([
            Promise.all(records.map((record) => record.closed)).then(() => true),
            new Promise((resolve) => { timer = setTimeout(() => resolve(false), 5000); }),
        ]);
        clearTimeout(timer);
        return closed;
    }

    async stopContext(context) {
        const rooms = [...this.rooms.values()].filter((room) => room.context === context);
        const results = await Promise.all(rooms.map((room) => this.stopRoom(context, room.password)));
        return results.every(Boolean);
    }

    async shutdown() {
        const rooms = [...this.rooms.values()];
        const results = await Promise.all(rooms.map((room) => this.stopRoom(room.context, room.password)));
        return results.every(Boolean);
    }
}

module.exports = { SkynetRuntime, buildSkynetCommand, GLOBAL_PROCESS_LIMIT };
