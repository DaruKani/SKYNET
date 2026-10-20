'use strict';

const fs = require('node:fs');
const path = require('node:path');

const AGENTS = new Set(['skynet', 'expert-oldschool']);

function defaultSkynetSettings() {
    return {
        executablePath: '',
        databasePath: '',
        version: 4962,
        maxProcesses: 4,
        duelTimeoutSeconds: 300,
        profiles: [],
    };
}

function boundedInteger(value, label, minimum, maximum) {
    const number = Number(value);
    if (!Number.isInteger(number) || number < minimum || number > maximum) {
        throw new Error(`${label} must be an integer from ${minimum} to ${maximum}`);
    }
    return number;
}

function explicitPath(value, label, extension) {
    const result = String(value || '').trim();
    if (result && (
        !path.isAbsolute(result) || result.length > 4000 || /[\x00-\x1f]/.test(result)
        || path.extname(result).toLowerCase() !== extension
    )) {
        throw new Error(`${label} must be an absolute ${extension} file path`);
    }
    return result;
}

function normalizeSkynetSettings(input) {
    const source = { ...defaultSkynetSettings(), ...input };
    if (!Array.isArray(source.profiles) || source.profiles.length > 200) {
        throw new Error('SKYNET profiles must be an array of at most 200 entries');
    }
    const ids = new Set();
    const profiles = source.profiles.map((profile) => {
        const id = String(profile?.id || '').trim();
        const label = String(profile?.label || '').trim();
        const agent = profile?.agent || 'skynet';
        if (!/^[A-Za-z0-9_-]{1,64}$/.test(id) || ids.has(id)) {
            throw new Error('SKYNET profile IDs must be unique letters, digits, underscores or hyphens (1-64 characters)');
        }
        ids.add(id);
        // Rank names are serialized in the 20-code-unit YGOPro player field.
        if (!label || label.length > 17 || /[\x00-\x1f]/.test(label)) {
            throw new Error(`SKYNET profile ${id}: label must contain 1-17 characters`);
        }
        if (!AGENTS.has(agent)) {
            throw new Error(`SKYNET profile ${id}: unsupported agent`);
        }
        const modelPath = explicitPath(profile.modelPath, `${id} model`, '.json');
        if (agent !== 'skynet' && modelPath) {
            throw new Error(`SKYNET profile ${id}: expert agents do not accept learned models`);
        }
        return {
            id,
            label,
            agent,
            deckPath: explicitPath(profile.deckPath, `${id} deck`, '.ydk'),
            modelPath,
        };
    });
    return {
        executablePath: explicitPath(source.executablePath, 'SKYNET executable', '.exe'),
        databasePath: explicitPath(source.databasePath, 'SKYNET card database', '.cdb'),
        version: boundedInteger(source.version, 'SKYNET protocol version', 1, 65535),
        maxProcesses: boundedInteger(source.maxProcesses, 'SKYNET process limit', 2, 32),
        duelTimeoutSeconds: boundedInteger(source.duelTimeoutSeconds, 'SKYNET duel timeout', 10, 3600),
        profiles,
    };
}

function requireFile(filename, label) {
    if (!filename || !fs.existsSync(filename) || !fs.statSync(filename).isFile()) {
        throw new Error(`${label} file does not exist: ${filename || '(not configured)'}`);
    }
}

function validateSkynetRuntime(instance) {
    const config = normalizeSkynetSettings(instance.skynet);
    requireFile(config.executablePath, 'SKYNET executable');
    if (path.basename(config.executablePath).toLowerCase() !== 'skynet.exe') {
        throw new Error('SKYNET executable must be named SKYNET.exe');
    }
    requireFile(config.databasePath, 'SKYNET card database');
    if (config.profiles.length === 0) {
        throw new Error('SKYNET needs at least one explicitly configured deck profile');
    }
    for (const profile of config.profiles) {
        requireFile(profile.deckPath, `SKYNET profile ${profile.id} deck`);
        if (profile.modelPath) requireFile(profile.modelPath, `SKYNET profile ${profile.id} model`);
    }
    return config;
}

function skynetCatalog(instance) {
    const config = validateSkynetRuntime(instance);
    return new Map(config.profiles.map((profile) => [profile.id, {
        aiLevel: null,
        deck: profile.id,
        dialog: null,
        label: profile.label,
        skynet: { ...config, profiles: undefined, profile },
    }]));
}

module.exports = { defaultSkynetSettings, normalizeSkynetSettings, skynetCatalog, validateSkynetRuntime };
