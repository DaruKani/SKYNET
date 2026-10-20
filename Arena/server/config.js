'use strict';

const path = require('path');

const rootDir = path.resolve(__dirname, '..');

function readPort(value) {
    const port = Number(value || 3000);
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
        throw new Error('SKYNET_ARENA_PORT must be a valid port (1–65535).');
    }
    return port;
}

function loadConfig() {
    const databaseValue = process.env.SKYNET_ARENA_DATABASE
        || process.env.WINDBOT_ARENA_DATABASE || path.join('data', 'arena.sqlite');
    return {
        databasePath: path.isAbsolute(databaseValue)
            ? databaseValue
            : path.resolve(rootDir, databaseValue),
        listenHost: process.env.SKYNET_ARENA_HOST || process.env.WINDBOT_ARENA_HOST || '127.0.0.1',
        listenPort: readPort(process.env.SKYNET_ARENA_PORT || process.env.WINDBOT_ARENA_PORT),
        rootDir,
    };
}

module.exports = { loadConfig };
