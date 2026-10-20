'use strict';

function calculateStats(raw) {
    const win = Number(raw?.win) || 0;
    const lose = Number(raw?.lose) || 0;
    const flee = Number(raw?.flee) || 0;
    const combo = Number(raw?.combo) || 0;
    // SRVPro counts disconnects separately; flee is not a third outcome exclusive of wins and losses.
    const games = win + lose;
    return {
        combo,
        flee,
        games,
        lose,
        win,
    };
}

function normalizeRank(rank) {
    if (!Array.isArray(rank)) {
        throw new Error('rank is not an array');
    }
    return rank.map((entry, index) => {
        if (!Array.isArray(entry) || entry.length !== 2 || typeof entry[0] !== 'string') {
            throw new Error(`rank[${index}] is not [name, statistics]`);
        }
        if (!entry[1] || typeof entry[1] !== 'object' || Array.isArray(entry[1])) {
            throw new Error(`Statistics in rank[${index}] are not an object`);
        }
        return [entry[0], calculateStats(entry[1])];
    });
}

module.exports = { calculateStats, normalizeRank };
