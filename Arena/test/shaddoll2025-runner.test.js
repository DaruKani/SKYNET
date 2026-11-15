'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const {
    parseArguments,
    printRunSummary,
    selectNormalOpponents,
} = require('../Tools/run_shaddoll2025_challenge');

const catalog = [
    { aiLevel: 4, deck: 'Classic' },
    { aiLevel: 3, deck: 'Lightsworn' },
    { aiLevel: 3, deck: 'Shaddoll2025' },
    { aiLevel: 3, deck: 'SkyStriker' },
    { aiLevel: 2, deck: 'Easy' },
];

test('Shaddoll runner defaults to one match against every other Normal bot', () => {
    assert.deepEqual(parseArguments([]), {
        arenaUrl: 'http://127.0.0.1:3000',
        gamesPerOpponent: 1,
        opponents: [],
        pollSeconds: 5,
        preflightOnly: false,
    });
    assert.deepEqual(selectNormalOpponents(catalog), ['Lightsworn', 'SkyStriker']);
});

test('Shaddoll runner accepts selected Normal opponents and rejects other levels', () => {
    const options = parseArguments([
        '--url', 'http://127.0.0.1:3137/',
        '--games', '3',
        '--opponent', 'Lightsworn',
        '--preflight-only',
    ]);
    assert.equal(options.arenaUrl, 'http://127.0.0.1:3137');
    assert.equal(options.gamesPerOpponent, 3);
    assert.deepEqual(options.opponents, ['Lightsworn']);
    assert.equal(options.preflightOnly, true);
    assert.deepEqual(selectNormalOpponents(catalog, options.opponents), ['Lightsworn']);
    assert.throws(() => selectNormalOpponents(catalog, ['Classic']), /not AI_LV3/);
});

test('Shaddoll runner requires a Normal-classified Shaddoll target', () => {
    assert.throws(
        () => selectNormalOpponents(catalog.filter((item) => item.deck !== 'Shaddoll2025')),
        /missing from Arena/,
    );
    assert.throws(
        () => selectNormalOpponents(catalog.map((item) => (
            item.deck === 'Shaddoll2025' ? { ...item, aiLevel: null } : item
        ))),
        /classified as AI_LV3/,
    );
});

test('Shaddoll result summary uses the observed matchup count', () => {
    const originalTable = console.table;
    let rows;
    console.table = (value) => { rows = value; };
    try {
        printRunSummary({
            matchups: [{
                label: 'Lightsworn',
                observedGames: 1,
                competitors: [
                    { source: 'target', win: 0, flee: 0 },
                    { source: 'opponent', win: 1, flee: 0 },
                ],
            }],
        });
    } finally {
        console.table = originalTable;
    }
    assert.equal(rows[0].observed, 1);
    assert.equal(rows[0].opponentWins, 1);
});
