<script setup>
import { computed, defineAsyncComponent, h, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import {
    NAlert,
    NAutoComplete,
    NButton,
    NButtonGroup,
    NCard,
    NCheckbox,
    NDropdown,
    NEmpty,
    NInput,
    NInputNumber,
    NModal,
    NPopconfirm,
    NProgress,
    NRadioButton,
    NRadioGroup,
    NSelect,
    NSpin,
    NSwitch,
    NTag,
    NTooltip,
    useMessage,
} from 'naive-ui';

const SettingsModal = defineAsyncComponent(() => import('./SettingsModal.vue'));

defineProps({
    darkMode: { type: Boolean, default: false },
});
const emit = defineEmits(['update:darkMode']);

const message = useMessage();
let storedRecentDecks = [];
try {
    const value = JSON.parse(localStorage.getItem('skynet-arena-recent-decks')
        || localStorage.getItem('windbot-arena-recent-decks') || '[]');
    if (Array.isArray(value)) {
        storedRecentDecks = [...new Set(value.filter((deck) => typeof deck === 'string'))];
    }
} catch {
    storedRecentDecks = [];
}
const system = ref(null);
const runs = ref([]);
const historyPage = ref(1);
const historyTotal = ref(0);
const deletingRunId = ref('');
const editingNoteRun = ref(null);
const noteDraft = ref('');
const savingNote = ref(false);
const activeRuns = ref([]);
const selectedRun = ref(null);
const selectedSrvproId = ref('');
const recentDecks = ref(storedRecentDecks);
const selectedDecks = ref([...storedRecentDecks]);
const allDecks = ref(false);
const experimentKind = ref('regression');
const targetDeck = ref('');
const challengerVersion = ref('current');
const deckListExpanded = ref(true);
const gamesPerMatchup = ref(20);
const stopAfterMinutes = ref(60);
const timerRunId = ref('');
const timerSaving = ref(false);
const stopConfirmationRunId = ref('');
const challengeGamesPerMatchup = ref(20);
const configuredGamesPerMatchup = computed({
    get: () => (
        experimentKind.value === 'challenge'
            ? challengeGamesPerMatchup.value
            : gamesPerMatchup.value
    ),
    set: (value) => {
        if (experimentKind.value === 'challenge') {
            challengeGamesPerMatchup.value = value;
        } else {
            gamesPerMatchup.value = value;
        }
    },
});
const loading = ref(true);
const starting = ref(false);
const stopping = ref(false);
const refreshingDecks = ref(false);
const liveConnected = ref(false);
const settingsOpen = ref(false);
const settingsRecord = ref(null);
const savingSettings = ref(false);
const inspectorOpen = ref(false);
const inspectorKind = ref('srvpro');
const inspectorLoading = ref(false);
const inspectorError = ref('');
const inspectorRooms = ref([]);
const inspectorHalfwayWatchEnabled = ref(null);
const updatingHalfwayWatch = ref(false);
const inspectorOutput = ref(null);
const clockNow = ref(Date.now());
let eventSource;
let clockTimer;
let inspectorTimer;
let inspectorRequest = 0;
let refreshing;
let revisionIndexes;
let runSelectionRequest = 0;
const pendingRefreshDomains = new Set();
let recentDecksValidated = false;
const historyPageSize = 10;
const allRefreshDomains = ['system', 'runs', 'active'];

const terminalStatuses = new Set(['completed', 'stopped', 'failed', 'interrupted']);
const statusLabels = {
    completed: 'Completed',
    failed: 'Failed',
    interrupted: 'Interrupted',
    preparing: 'Preparing',
    running: 'Running',
    settling: 'Finishing',
    stopped: 'Stopped',
    stopping: 'Stopping',
};
const statusTypes = {
    completed: 'success',
    failed: 'error',
    interrupted: 'warning',
    preparing: 'info',
    running: 'success',
    settling: 'warning',
    stopped: 'default',
    stopping: 'warning',
};
const runKindLabels = {
    regression: 'Regression',
    challenge: 'Challenge',
    ranking: 'Ranking',
    tag: 'Tag smoke test',
};
const runKindIndexes = {
    regression: '01',
    challenge: '02',
    tag: '03',
    ranking: '04',
};
const runKindDescriptions = {
    regression: 'Compare candidate and baseline policies on matching deck profiles for a fixed number of duels.',
    challenge: 'Challenge selected opponents in sequence with one deck. Each matchup ends after the requested number of duels.',
    tag: 'Form random tag teams until stopped. Room creation runs at half speed; this checks stability without scoring wins.',
    ranking: 'Play random pairings from the selected decks until stopped.',
};

const regressionDeckOptions = computed(() => (system.value?.configuration.decks || []).map((item) => ({
    aiLevel: item.aiLevel,
    currentLabel: item.currentLabel,
    value: item.deck,
})));
const currentDeckOptions = computed(() => (system.value?.configuration.currentDecks || []).map((item) => ({
    aiLevel: item.aiLevel,
    currentLabel: item.currentLabel,
    value: item.deck,
})));
const oldDeckOptions = computed(() => (system.value?.configuration.oldDecks || []).map((item) => ({
    aiLevel: item.aiLevel,
    currentLabel: item.currentLabel,
    value: item.deck,
})));
const catalogDeckOptions = computed(() => (
    experimentKind.value === 'regression' ? regressionDeckOptions.value : currentDeckOptions.value
));
const targetDeckOptions = computed(() => {
    const query = String(targetDeck.value || '').trim().toLocaleLowerCase();
    const options = challengerVersion.value === 'old' ? oldDeckOptions.value : currentDeckOptions.value;
    return options
        .filter((option) => (
            query === ''
            || option.value.toLocaleLowerCase().includes(query)
            || option.currentLabel.toLocaleLowerCase().includes(query)
        ))
        .map((option) => ({
            displayLabel: option.currentLabel === option.value
                ? option.value
                : `${option.value} — ${option.currentLabel}`,
            label: option.value,
            value: option.value,
        }));
});
const recentDeckSet = computed(() => new Set(recentDecks.value));
const deckOptions = computed(() => {
    const recentOrder = new Map(recentDecks.value.map((deck, index) => [deck, index]));
    return [...catalogDeckOptions.value].sort((left, right) => {
        const leftIsRecent = recentOrder.has(left.value);
        const rightIsRecent = recentOrder.has(right.value);
        if (leftIsRecent && rightIsRecent) {
            return recentOrder.get(left.value) - recentOrder.get(right.value);
        }
        if (leftIsRecent !== rightIsRecent) {
            return leftIsRecent ? -1 : 1;
        }
        return 0;
    });
});
const bulkDeckCount = computed(() => deckOptions.value.filter((item) => item.aiLevel !== 1).length);
const availableAiLevels = computed(() => [...new Set(
    deckOptions.value
        .map((option) => option.aiLevel)
        .filter((level) => Number.isInteger(level) || level === null),
)].sort((left, right) => {
    if (left === null) {
        return 1;
    }
    if (right === null) {
        return -1;
    }
    return right - left;
}));
const effectiveSelectedDeckSet = computed(() => {
    if (!allDecks.value) {
        return new Set(selectedDecks.value);
    }
    const target = String(targetDeck.value || '').trim();
    return new Set(deckOptions.value
        .filter((option) => (
            option.aiLevel !== 1
            || (experimentKind.value === 'challenge' && option.value === target)
        ))
        .map((option) => option.value));
});
const allDeckCount = computed(() => {
    if (experimentKind.value !== 'challenge' || !String(targetDeck.value || '').trim()) {
        return bulkDeckCount.value;
    }
    const included = deckOptions.value.some((item) => (
        item.value === String(targetDeck.value || '').trim() && item.aiLevel !== 1
    ));
    return bulkDeckCount.value + (included ? 0 : 1);
});
const displayedRun = computed(() => selectedRun.value);
const historyPageCount = computed(() => Math.max(1, Math.ceil(
    historyTotal.value / historyPageSize,
)));
const historyPlaceholderCount = computed(() => (
    historyPageCount.value > 1 && historyPage.value === historyPageCount.value
        ? Math.max(0, historyPageSize - runs.value.length)
        : 0
));
const selectedSrvpro = computed(() => (
    system.value?.srvpros.find((srvpro) => srvpro.id === selectedSrvproId.value) || null
));
const selectedSrvproDisplay = computed(() => (
    selectedSrvpro.value
        ? `${selectedSrvpro.value.name} · ${selectedSrvpro.value.host}:${selectedSrvpro.value.duelPort}`
        : '—'
));
const activeRun = computed(() => (
    activeRuns.value.find((run) => run.srvproId === selectedSrvproId.value) || null
));
const launchPanelState = computed(() => {
    if (
        activeRun.value
        && displayedRun.value?.id !== activeRun.value.id
        && terminalStatuses.has(displayedRun.value?.status)
    ) {
        return 'terminal';
    }
    return activeRun.value ? 'running' : 'ready';
});
const terminalRunActionLabel = computed(() => (
    `Experiment ${statusLabels[displayedRun.value?.status] || 'Finished'}`
));
const stopCountdown = computed(() => {
    const run = activeRun.value;
    if (run?.status !== 'running' || !run.config?.stopAt) return '';
    const seconds = Math.max(0, Math.ceil((Date.parse(run.config.stopAt) - clockNow.value) / 1000));
    return [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60]
        .map((value) => String(value).padStart(2, '0')).join(':');
});
const stopMenuOptions = computed(() => {
    const run = activeRun.value;
    const options = [];
    if (stopCountdown.value) {
        options.push({ label: 'Stop immediately', key: 'immediate', disabled: stopping.value });
    }
    options.push({ label: 'Finish active duels', key: 'graceful', disabled: stopping.value || run?.status !== 'running',
        description: 'Stop creating rooms and let all active duels finish before collecting results.' });
    if (['ranking', 'tag'].includes(run?.kind)) {
        options.push({ label: stopCountdown.value ? 'Edit stop timer' : 'Stop timer', key: 'timer',
            disabled: stopping.value || run?.status !== 'running',
            description: 'Finish active duels when the timer expires.' });
    }
    return options;
});
const hasActiveRuns = computed(() => activeRuns.value.length > 0);
const srvproOptions = computed(() => (system.value?.srvpros || []).map((srvpro) => {
    const busy = activeRuns.value.some((run) => run.srvproId === srvpro.id);
    return {
        disabled: busy,
        label: `${srvpro.name} — ${srvpro.host || 'Not configured'}:${srvpro.duelPort}${busy ? ' (busy)' : ''}`,
        value: srvpro.id,
    };
}));
const modeConfiguration = computed(() => (
    system.value?.configurationBySrvpro?.[selectedSrvproId.value]?.modes?.[
        experimentKind.value === 'challenge' && challengerVersion.value === 'old'
            ? 'challengeOld'
            : experimentKind.value
    ]
    || { issues: system.value?.configuration.issues || [], valid: system.value?.configuration.valid === true }
));
const configurationReady = computed(() => modeConfiguration.value.valid === true);
const inspectorTitle = computed(() => (
    inspectorKind.value === 'srvpro'
        ? `${selectedSrvpro.value?.name || 'SRVPro'} Rooms (${inspectorRooms.value.length})`
        : `${inspectorKind.value === 'current' ? 'Candidate' : 'Baseline'} agent output`
));
const canStart = computed(() => {
    if (!selectedSrvpro.value || !configurationReady.value || activeRun.value) {
        return false;
    }
    const selectedCount = allDecks.value ? bulkDeckCount.value : new Set(selectedDecks.value).size;
    if (experimentKind.value === 'challenge') {
        return String(targetDeck.value || '').trim() !== ''
            && (allDecks.value || selectedCount > 0)
            && Number.isInteger(challengeGamesPerMatchup.value);
    }
    if (experimentKind.value === 'ranking') {
        return selectedCount >= 2;
    }
    if (experimentKind.value === 'tag') {
        return selectedCount >= 1;
    }
    return selectedCount > 0 && Number.isInteger(gamesPerMatchup.value);
});
const progress = computed(() => {
    const run = displayedRun.value;
    if (!run || run.totalGames === 0) {
        return 0;
    }
    const observedGames = Math.min(run.observedGames || 0, run.totalGames);
    return Math.min(100, Math.round((observedGames / run.totalGames) * 100));
});
const totals = computed(() => {
    const result = [
        { flee: 0, games: 0, lose: 0, win: 0 },
        { flee: 0, games: 0, lose: 0, win: 0 },
    ];
    for (const matchup of displayedRun.value?.matchups || []) {
        matchup.competitors.forEach((competitor, index) => {
            result[index].win += competitor.win;
            result[index].lose += competitor.lose;
            result[index].flee += competitor.flee;
            result[index].games += competitor.games;
        });
    }
    if (displayedRun.value?.kind === 'challenge') {
        result[0].flee = displayedRun.value.challengeTargetFlee || 0;
    }
    return result;
});
const currentWinRate = computed(() => {
    const decided = totals.value[0].win + totals.value[1].win;
    return decided === 0 ? 0 : totals.value[0].win / decided;
});
const regressionRows = computed(() => {
    if (displayedRun.value?.kind !== 'regression') {
        return [];
    }
    return [...displayedRun.value.matchups].sort((left, right) => (
        (left.observedGames === 0) - (right.observedGames === 0)
        || left.currentWinRate - right.currentWinRate
        || (left.competitors[0]?.win || 0) - (right.competitors[0]?.win || 0)
        || left.label.localeCompare(right.label)
    ));
});
const challengeRows = computed(() => {
    if (displayedRun.value?.kind !== 'challenge') {
        return [];
    }
    return [...displayedRun.value.matchups].sort((left, right) => (
        (left.observedGames === 0) - (right.observedGames === 0)
        || left.currentWinRate - right.currentWinRate
        || (left.competitors[0]?.win || 0) - (right.competitors[0]?.win || 0)
        || left.label.localeCompare(right.label)
    ));
});
const rankingRows = computed(() => {
    if (displayedRun.value?.kind !== 'ranking') {
        return [];
    }
    return [...displayedRun.value.matchups].sort((left, right) => (
        right.currentWinRate - left.currentWinRate
        || (right.competitors[0]?.win || 0) - (left.competitors[0]?.win || 0)
        || left.label.localeCompare(right.label)
    ));
});
const tagRows = computed(() => {
    if (displayedRun.value?.kind !== 'tag') {
        return [];
    }
    return [...displayedRun.value.matchups].sort((left, right) => (
        right.launchedGames - left.launchedGames || left.label.localeCompare(right.label)
    ));
});
const tagWindBotOutputScannable = computed(() => (
    displayedRun.value?.kind === 'tag'
    && displayedRun.value.config?.windbots?.current?.mode === 'local'
));
const tagIssueCount = computed(() => Number(displayedRun.value?.windbotOutputErrorCount) || 0);
const regressionRetestDecks = computed(() => {
    const availableDecks = new Set(regressionDeckOptions.value.map((option) => option.value));
    return regressionRows.value
        .filter((matchup) => (
            matchup.observedGames >= 100
            && (matchup.currentWinRate > 0.55 || matchup.currentWinRate < 0.45)
            && availableDecks.has(matchup.label)
        ))
        .map((matchup) => matchup.label);
});
const rankingLeader = computed(() => rankingRows.value.find((item) => item.observedGames > 0) || null);
const resultDownloadOptions = [
    { label: 'Download text', key: 'text' },
    { label: 'Download Markdown', key: 'markdown' },
    { label: 'Download JSON', key: 'json' },
];
const elapsedMilliseconds = computed(() => {
    return getRunElapsedMilliseconds(displayedRun.value);
});
const estimatedCompletionAt = computed(() => {
    const run = displayedRun.value;
    if (!run || run.kind === 'ranking' || run.kind === 'tag') {
        return null;
    }
    if (terminalStatuses.has(run.status)) {
        return run.finishedAt || null;
    }
    const startedAt = Date.parse(run.startedAt);
    const sampledAt = Date.parse(run.latestRankAt);
    const observedGames = Math.min(run.observedGames || 0, run.totalGames || 0);
    const elapsed = sampledAt - startedAt;
    const minimumSample = Math.min(
        run.totalGames || 0,
        Math.max(20, Math.ceil((run.totalGames || 0) * 0.05)),
    );
    if (
        !Number.isFinite(startedAt)
        || !Number.isFinite(sampledAt)
        || run.totalGames === 0
        || observedGames < minimumSample
        || elapsed < 30000
    ) {
        return null;
    }
    const estimatedDuration = elapsed * (run.totalGames / observedGames);
    return new Date(startedAt + estimatedDuration).toISOString();
});

function formatWindBotEndpoint(windbot) {
    if (!windbot) {
        return '—';
    }
    if (windbot.mode === 'skynet') {
        return 'SKYNET · local duel processes';
    }
    return `${windbot.mode === 'local' ? 'Local' : 'Remote'} ${windbot.host}:${windbot.port}`;
}

async function api(path, options) {
    const response = await fetch(path, {
        cache: 'no-store',
        headers: options?.body ? { 'Content-Type': 'application/json' } : undefined,
        ...options,
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(body.error || `Request failed: HTTP ${response.status}`);
    }
    return body;
}

async function applyRunConfiguration(run) {
    experimentKind.value = run.kind;
    await nextTick();
    if (system.value?.srvpros.some((srvpro) => srvpro.id === run.srvproId)) {
        selectedSrvproId.value = run.srvproId;
    }
    allDecks.value = run.config?.selection === 'all';
    selectedDecks.value = run.matchups.map((matchup) => matchup.label);
    targetDeck.value = run.kind === 'challenge' ? run.config?.targetDeck || '' : '';
    challengerVersion.value = run.kind === 'challenge'
        ? run.config?.challengerVersion || 'current'
        : 'current';
    if (run.gamesPerMatchup > 0) {
        if (run.kind === 'challenge') {
            challengeGamesPerMatchup.value = run.gamesPerMatchup;
        } else if (run.kind === 'regression') {
            gamesPerMatchup.value = run.gamesPerMatchup;
        }
    }
}

async function refresh(domains = allRefreshDomains) {
    domains.forEach((domain) => pendingRefreshDomains.add(domain));
    if (refreshing) {
        return refreshing;
    }
    refreshing = (async () => {
        while (pendingRefreshDomains.size > 0) {
            const requested = [...pendingRefreshDomains];
            pendingRefreshDomains.clear();
            const responses = Object.fromEntries(await Promise.all(requested.map(async (domain) => {
                if (domain === 'system') {
                    return [domain, await api('/api/system')];
                }
                if (domain === 'runs') {
                    return [domain, await api(
                        `/api/runs?limit=${historyPageSize}&offset=${(historyPage.value - 1) * historyPageSize}`,
                    )];
                }
                return [domain, await api('/api/runs/active')];
            })));
            if (responses.system) {
                system.value = responses.system;
                if (!system.value.srvpros.some((srvpro) => srvpro.id === selectedSrvproId.value)) {
                    selectedSrvproId.value = system.value.srvpros[0]?.id || '';
                }
            }
            if (responses.runs) {
                runs.value = responses.runs.runs;
                historyTotal.value = responses.runs.total;
                const selectedHistory = runs.value.find((run) => run.id === selectedRun.value?.id);
                if (selectedHistory) {
                    selectedRun.value.note = selectedHistory.note;
                }
            }
            if (responses.active) {
                const previousActiveIds = new Set(activeRuns.value.map((run) => run.id));
                const selectedRunId = selectedRun.value?.id;
                const nextActiveRuns = responses.active.runs;
                activeRuns.value = nextActiveRuns;
                const selectedActiveRun = nextActiveRuns.find((run) => run.id === selectedRunId);
                if (selectedActiveRun) {
                    selectedRun.value = selectedActiveRun;
                } else if (selectedRunId && previousActiveIds.has(selectedRunId)) {
                    const latest = await api(`/api/runs/${selectedRunId}`).catch(() => null);
                    selectedRun.value = latest?.run || null;
                } else if (!selectedRun.value && nextActiveRuns.length > 0) {
                    selectedRun.value = nextActiveRuns[0];
                    await applyRunConfiguration(nextActiveRuns[0]);
                }
            }
            if (
                responses.runs
                && selectedRun.value
                && terminalStatuses.has(selectedRun.value.status)
            ) {
                const selectedRunId = selectedRun.value.id;
                const selectionRequest = runSelectionRequest;
                const { run } = await api(`/api/runs/${selectedRunId}`);
                if (selectionRequest === runSelectionRequest && selectedRun.value?.id === selectedRunId) {
                    selectedRun.value = run;
                }
            }
            if (!selectedRun.value && runs.value.length > 0) {
                const initialRun = (await api(`/api/runs/${runs.value[0].id}`)).run;
                selectedRun.value = initialRun;
                await applyRunConfiguration(initialRun);
            }
        }
    })().finally(() => {
        refreshing = null;
        loading.value = false;
    });
    return refreshing;
}

function handleRevisionEvent(event) {
    let next;
    try {
        next = JSON.parse(event.data).revisions;
    } catch {
        return;
    }
    if (!next || !allRefreshDomains.every((domain) => Number.isInteger(next[domain]))) {
        return;
    }
    liveConnected.value = true;
    const changedDomains = revisionIndexes && system.value
        ? allRefreshDomains.filter((domain) => next[domain] !== revisionIndexes[domain])
        : allRefreshDomains;
    if (changedDomains.length === 0) {
        return;
    }
    refresh(changedDomains).then(() => {
        revisionIndexes ||= {};
        changedDomains.forEach((domain) => {
            revisionIndexes[domain] = next[domain];
        });
    }).catch((error) => message.error(error.message));
}

async function selectRun(runId) {
    const request = ++runSelectionRequest;
    try {
        const run = (await api(`/api/runs/${runId}`)).run;
        if (request !== runSelectionRequest) {
            return;
        }
        selectedRun.value = run;
        await applyRunConfiguration(run);
    } catch (error) {
        if (request === runSelectionRequest) {
            message.error(error.message);
        }
    }
}

function selectSrvpro(srvproId) {
    if (activeRuns.value.some((run) => run.srvproId === srvproId)) {
        return;
    }
    runSelectionRequest += 1;
    selectedSrvproId.value = srvproId;
}

function selectHistoryPage(page) {
    historyPage.value = page;
    refresh(['runs']).catch((error) => message.error(error.message));
}

async function saveRunNote() {
    if (savingNote.value) return;
    savingNote.value = true;
    try {
        const { run } = await api(`/api/runs/${editingNoteRun.value.id}/note`, {
            method: 'PUT',
            body: JSON.stringify({ note: noteDraft.value }),
        });
        if (selectedRun.value?.id === run.id) selectedRun.value.note = run.note;
        editingNoteRun.value = null;
        await refresh(['runs', 'active']);
        message.success('Note saved');
    } catch (error) {
        message.error(error.message);
    } finally {
        savingNote.value = false;
    }
}

async function deleteRun(runId) {
    deletingRunId.value = runId;
    try {
        await api(`/api/runs/${runId}`, { method: 'DELETE' });
        if (selectedRun.value?.id === runId) {
            runSelectionRequest += 1;
            selectedRun.value = null;
        }
        const remainingTotal = Math.max(0, historyTotal.value - 1);
        const lastPage = Math.max(1, Math.ceil(remainingTotal / historyPageSize));
        historyPage.value = Math.min(historyPage.value, lastPage);
        await refresh(['runs', 'active']);
        message.success('Experiment history deleted');
    } catch (error) {
        message.error(error.message);
    } finally {
        deletingRunId.value = '';
    }
}

function shouldConfirmRunDeletion(run) {
    const shortTerminalRun = (run.status === 'stopped' || run.status === 'interrupted')
        && getRunElapsedMilliseconds(run) <= 30_000;
    return run.status !== 'failed'
        && !shortTerminalRun;
}

async function openSettings() {
    try {
        settingsRecord.value = await api('/api/settings');
        settingsOpen.value = true;
    } catch (error) {
        message.error(error.message);
    }
}

async function loadInspector(silent = false) {
    const kind = inspectorKind.value;
    if (kind === 'srvpro' && updatingHalfwayWatch.value) {
        return;
    }
    const request = ++inspectorRequest;
    if (!silent) {
        inspectorLoading.value = true;
    }
    try {
        const body = kind === 'srvpro'
            ? await api(`/api/srvpro/rooms?srvproId=${encodeURIComponent(selectedSrvproId.value)}`)
            : await api(`/api/windbots/${kind}/output`);
        if (request !== inspectorRequest || kind !== inspectorKind.value || !inspectorOpen.value) {
            return;
        }
        inspectorError.value = '';
        if (kind === 'srvpro') {
            inspectorRooms.value = body.rooms;
            inspectorHalfwayWatchEnabled.value = body.enableHalfwayWatch;
        } else {
            inspectorOutput.value = body;
        }
    } catch (error) {
        if (kind === inspectorKind.value && inspectorOpen.value) {
            inspectorError.value = error.message;
        }
    } finally {
        if (kind === inspectorKind.value) {
            inspectorLoading.value = false;
        }
    }
}

function openInspector(kind) {
    clearInterval(inspectorTimer);
    inspectorKind.value = kind;
    inspectorRooms.value = [];
    inspectorHalfwayWatchEnabled.value = null;
    inspectorOutput.value = null;
    inspectorError.value = '';
    inspectorOpen.value = true;
    loadInspector();
    inspectorTimer = setInterval(() => loadInspector(true), 2000);
}

function closeInspector() {
    inspectorOpen.value = false;
    inspectorRequest++;
    clearInterval(inspectorTimer);
}

async function updateHalfwayWatch(enabled) {
    updatingHalfwayWatch.value = true;
    const request = ++inspectorRequest;
    const srvproId = selectedSrvproId.value;
    try {
        const result = await api('/api/srvpro/halfwaywatch', {
            body: JSON.stringify({
                enabled,
                srvproId,
            }),
            method: 'PUT',
        });
        if (
            request !== inspectorRequest
            || !inspectorOpen.value
            || inspectorKind.value !== 'srvpro'
            || selectedSrvproId.value !== srvproId
        ) {
            return;
        }
        inspectorHalfwayWatchEnabled.value = result.enableHalfwayWatch;
        message.success(result.enableHalfwayWatch ? 'Spectating enabled for new rooms' : 'Spectating disabled for new rooms');
    } catch (error) {
        if (
            request === inspectorRequest
            && inspectorOpen.value
            && inspectorKind.value === 'srvpro'
            && selectedSrvproId.value === srvproId
        ) {
            message.error(error.message);
        }
    } finally {
        updatingHalfwayWatch.value = false;
    }
}

function formatRoomPlayer(player, includeScore = true) {
    if (!player) {
        return '—';
    }
    const details = [];
    if (includeScore && player.status?.score !== null && player.status?.score !== undefined) {
        details.push(`Score: ${player.status.score}`);
    }
    if (player.status?.lp !== null && player.status?.lp !== undefined) {
        details.push(`LP: ${player.status.lp}`);
    }
    return details.length > 0 ? `${player.name} (${details.join(' ')})` : player.name;
}

function getRoomPlayer(room, position) {
    return room.players.find((player) => player.position === position);
}

async function saveSettings(settings) {
    savingSettings.value = true;
    try {
        settingsRecord.value = await api('/api/settings', {
            body: JSON.stringify(settings),
            method: 'PUT',
        });
        settingsOpen.value = false;
        message.success('Settings saved');
        await refresh(['system']);
    } catch (error) {
        message.error(error.message);
    } finally {
        savingSettings.value = false;
    }
}

async function refreshBotConfigs() {
    refreshingDecks.value = true;
    try {
        const result = await api('/api/decks/refresh', { method: 'POST' });
        const fingerprintChanged = JSON.stringify(system.value?.botConfigFingerprints || null)
            !== JSON.stringify(result.botConfigFingerprints);
        const catalogChanged = JSON.stringify(system.value?.configuration || null)
            !== JSON.stringify(result.configuration);
        message.success(`Catalog refreshed: ${
            result.contentChanged || fingerprintChanged || catalogChanged ? 'changed' : 'unchanged'
        }`);
        await refresh(['system']);
    } catch (error) {
        message.error(error.message);
    } finally {
        refreshingDecks.value = false;
    }
}

async function startRun() {
    starting.value = true;
    try {
        const requestedDecks = allDecks.value ? [] : [...selectedDecks.value];
        const request = {
            decks: requestedDecks,
            kind: experimentKind.value,
            srvproId: selectedSrvproId.value,
        };
        if (experimentKind.value === 'regression') {
            request.gamesPerMatchup = gamesPerMatchup.value;
        } else if (experimentKind.value === 'challenge') {
            request.targetDeck = String(targetDeck.value || '').trim();
            request.challengerVersion = challengerVersion.value;
            request.gamesPerMatchup = challengeGamesPerMatchup.value;
        }
        const body = await api('/api/runs', {
            body: JSON.stringify(request),
            method: 'POST',
        });
        if (requestedDecks.length > 0) {
            const selected = new Set(requestedDecks);
            recentDecks.value = catalogDeckOptions.value
                .filter((option) => selected.has(option.value))
                .map((option) => option.value);
            try {
                localStorage.setItem('skynet-arena-recent-decks', JSON.stringify(recentDecks.value));
            } catch {
                // Recent selections remain available until this page closes.
            }
        }
        runSelectionRequest += 1;
        activeRuns.value = [
            body.run,
            ...activeRuns.value.filter((run) => run.id !== body.run.id),
        ];
        selectedRun.value = body.run;
        historyPage.value = 1;
        message.success(`${runKindLabels[experimentKind.value]} created`);
        await refresh(['runs', 'active']);
    } catch (error) {
        message.error(error.message);
    } finally {
        starting.value = false;
    }
}

function handleStopAction(key) {
    if (!activeRun.value) return;
    if (key === 'timer') {
        timerRunId.value = activeRun.value.id;
        stopAfterMinutes.value = activeRun.value.config?.stopAt
            ? Math.max(1, Math.ceil((Date.parse(activeRun.value.config.stopAt) - Date.now()) / 60_000))
            : 60;
    } else if (key === 'immediate') {
        stopConfirmationRunId.value = activeRun.value.id;
    } else {
        stopRun(true);
    }
}

async function saveScheduledStop() {
    timerSaving.value = true;
    try {
        await api(`/api/runs/${timerRunId.value}/schedule-stop`, {
            method: 'POST', body: JSON.stringify({ minutes: stopAfterMinutes.value }),
        });
        timerRunId.value = '';
        clockNow.value = Date.now();
        await refresh(['runs', 'active']);
        message.success('Stop timer scheduled');
    } catch (error) {
        message.error(error.message);
    } finally {
        timerSaving.value = false;
    }
    return false;
}

async function stopRun(graceful = false, runId = activeRun.value?.id) {
    if (!runId) {
        return;
    }
    stopping.value = true;
    try {
        await api(`/api/runs/${runId}/${graceful ? 'graceful-stop' : 'stop'}`, { method: 'POST' });
        message.info(graceful ? 'Stop requested. Waiting for active duels before collecting results.' : 'Collecting results and stopping the experiment.');
        await refresh(['runs', 'active']);
    } catch (error) {
        message.error(error.message);
    } finally {
        stopping.value = false;
    }
}

function formatPercent(value) {
    return `${((Number(value) || 0) * 100).toFixed(1)}%`;
}

function formatDate(value, timeOnly = false) {
    if (!value) {
        return '—';
    }
    return new Intl.DateTimeFormat('en-GB', timeOnly ? {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
    } : {
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    }).format(new Date(value));
}

function formatDuration(value) {
    if (!Number.isFinite(value)) {
        return '—';
    }
    const totalSeconds = Math.floor(value / 1000);
    const days = Math.floor(totalSeconds / 86400);
    const hours = Math.floor((totalSeconds % 86400) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    if (days > 0) {
        return `${days}d ${hours}h`;
    }
    if (hours > 0) {
        return `${hours}h ${minutes}m`;
    }
    if (minutes > 0) {
        return `${minutes}m ${seconds}s`;
    }
    return `${seconds}s`;
}

function getRunElapsedMilliseconds(run) {
    const createdAt = Date.parse(run?.createdAt);
    if (!Number.isFinite(createdAt)) {
        return null;
    }
    const finishedAt = terminalStatuses.has(run.status) ? Date.parse(run.finishedAt) : clockNow.value;
    const end = Number.isFinite(finishedAt) ? finishedAt : clockNow.value;
    return Math.max(0, end - createdAt);
}

function formatHistoryTitle(run) {
    const label = runKindLabels[run.kind] || run.kind;
    return `${label} · ${run.note || formatHistoryNote(run)}`;
}

function formatHistoryNote(run) {
    const matchupCount = run.matchupCount ?? run.matchups?.length ?? 0;
    if (run.kind === 'challenge') {
        const version = run.config?.challengerVersion === 'old' ? 'Baseline' : 'Candidate';
        return `${version} ${run.config?.targetDeck || '—'} VS ${matchupCount} decks`;
    }
    const deckName = run.deckName || run.matchups?.[0]?.label;
    if (run.kind === 'regression' && matchupCount === 1 && deckName) {
        return `${deckName} · 1 decks`;
    }
    return `${matchupCount} decks`;
}

async function writeClipboard(text) {
    if (navigator.clipboard?.writeText) {
        try {
            await navigator.clipboard.writeText(text);
            return;
        } catch {
            // Use the fallback clipboard method on insecure origins.
        }
    }
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    const copied = document.execCommand('copy');
    textarea.remove();
    if (!copied) {
        throw new Error('The browser denied clipboard access');
    }
}

async function copyRoomName(name) {
    try {
        await writeClipboard(name);
        message.success('Room name copied');
    } catch (error) {
        message.error(`Copy failed: ${error.message}`);
    }
}

function serializeResults(run, format) {
    if (format === 'json') {
        return JSON.stringify({
            exportedAt: new Date().toISOString(),
            run: {
                id: run.id,
                kind: run.kind,
                kindLabel: runKindLabels[run.kind] || 'Results',
                status: run.status,
                statusLabel: statusLabels[run.status] || run.status,
                createdAt: run.createdAt,
                startedAt: run.startedAt,
                finishedAt: run.finishedAt,
                observedGames: run.observedGames,
                launchedGames: run.launchedGames,
                totalGames: run.totalGames,
                gamesPerMatchup: run.gamesPerMatchup,
                config: run.config,
            },
            summary: {
                elapsedMilliseconds: elapsedMilliseconds.value,
                currentWinRate: ['ranking', 'tag'].includes(run.kind) ? null : currentWinRate.value,
                windbotOutputErrorCount: run.kind === 'tag'
                    ? run.windbotOutputErrorCount || 0
                    : null,
                windbotOutputScannable: run.kind === 'tag'
                    ? run.config?.windbots?.current?.mode === 'local'
                    : null,
                rankingLeader: run.kind === 'ranking' && rankingLeader.value ? {
                    deck: rankingLeader.value.label,
                    winRate: rankingLeader.value.currentWinRate,
                } : null,
            },
            matchups: run.matchups,
        }, null, 2);
    }

    const summary = [
        ['Experiment ID', run.id],
        ['Status', statusLabels[run.status] || run.status],
        ['Created at', formatDate(run.createdAt)],
        ['Completed duels', run.kind === 'tag' ? 'Not scored' : run.observedGames],
        ['Launched duels', `${run.launchedGames}${
            !['ranking', 'tag'].includes(run.kind) ? ` / ${run.totalGames}` : ''
        }`],
    ];
    if (run.kind === 'ranking') {
        summary.push(['Leading deck', rankingLeader.value
            ? `${rankingLeader.value.label} (${formatPercent(rankingLeader.value.currentWinRate)})`
            : 'Collecting']);
    } else if (run.kind === 'tag') {
        summary.push(['Test decks', run.matchups.length]);
        summary.push([
            'Agent errors',
            tagWindBotOutputScannable.value ? tagIssueCount.value : 'Remote output unavailable',
        ]);
    } else {
        if (run.kind === 'challenge') {
            summary.push(['Challenger deck', run.config?.targetDeck || '—']);
            summary.push(['Challenger policy', run.config?.challengerVersion === 'old' ? 'Baseline' : 'Candidate']);
            if (run.challengeTargetFlee > 0) {
                summary.push(['Challenger disconnects', run.challengeTargetFlee]);
            }
        }
        summary.push([
            `${run.kind === 'challenge' ? 'Challenger deck' : 'Candidate'} win rate`,
            formatPercent(currentWinRate.value),
        ]);
    }
    summary.push(['Elapsed time', formatDuration(elapsedMilliseconds.value)]);

    let columns;
    let rows;
    if (run.kind === 'regression') {
        columns = ['Deck', 'Level / Bot', 'Results collected', 'W / L / disconnects', 'Candidate win rate'];
        rows = regressionRows.value.map((matchup) => {
            const competitor = matchup.competitors[0];
            return [
                matchup.label,
                `${matchup.aiLevel ? `LV${matchup.aiLevel} / ` : ''}${competitor?.botLabel || '—'}`,
                `${matchup.observedGames} / ${matchup.targetGames}`,
                `${competitor?.win || 0} / ${competitor?.lose || 0} / ${competitor?.flee || 0}`,
                formatPercent(matchup.currentWinRate),
            ];
        });
    } else if (run.kind === 'challenge') {
        columns = ['Opponent decks', 'Level / Bot', 'Results collected', 'Challenger W / L', 'Opponent disconnects', 'Challenger win rate'];
        rows = challengeRows.value.map((matchup) => {
            const competitor = matchup.competitors[0];
            return [
                matchup.label,
                `${matchup.aiLevel ? `LV${matchup.aiLevel} / ` : ''}${matchup.competitors[1]?.botLabel || '—'}`,
                `${matchup.observedGames} / ${matchup.targetGames}`,
                `${competitor?.win || 0} / ${competitor?.lose || 0}`,
                matchup.competitors[1]?.flee || 0,
                formatPercent(matchup.currentWinRate),
            ];
        });
    } else if (run.kind === 'tag') {
        columns = ['Test decks', 'Level / Bot', 'Duels played'];
        rows = tagRows.value.map((entry) => [
            entry.label,
            `${entry.aiLevel ? `LV${entry.aiLevel} / ` : ''}${entry.competitors[0]?.botLabel || '—'}`,
            entry.launchedGames,
        ]);
    } else {
        columns = ['Rank', 'Deck', 'Level / Bot', 'Collected / launched', 'W / L / disconnects', 'Win rate'];
        rows = rankingRows.value.map((entry, index) => {
            const competitor = entry.competitors[0];
            return [
                index + 1,
                entry.label,
                `${entry.aiLevel ? `LV${entry.aiLevel} / ` : ''}${competitor?.botLabel || '—'}`,
                `${entry.observedGames} / ${entry.launchedGames}`,
                `${competitor?.win || 0} / ${competitor?.lose || 0} / ${competitor?.flee || 0}`,
                formatPercent(entry.currentWinRate),
            ];
        });
    }

    if (format === 'markdown') {
        const escapeCell = (value) => String(value).replaceAll('|', '\\|').replaceAll('\n', '<br>');
        return [
            `# Skynet Arena · ${runKindLabels[run.kind] || 'Results'}`,
            '',
            ...summary.map(([label, value]) => `- ${label}: ${value}`),
            '',
            `| ${columns.map(escapeCell).join(' | ')} |`,
            `| ${columns.map(() => '---').join(' | ')} |`,
            ...rows.map((row) => `| ${row.map(escapeCell).join(' | ')} |`),
            '',
        ].join('\n');
    }

    return [
        `Skynet Arena · ${runKindLabels[run.kind] || 'Results'}`,
        ...summary.map(([label, value]) => `${label}: ${value}`),
        '',
        columns.join('\t'),
        ...rows.map((row) => row.join('\t')),
    ].join('\n');
}

async function copyResults() {
    const run = displayedRun.value;
    if (!run) {
        return;
    }

    try {
        await writeClipboard(serializeResults(run, 'text'));
        message.success('Results copied as text');
    } catch (error) {
        message.error(`Copy failed: ${error.message}`);
    }
}

function downloadResults(format) {
    const run = displayedRun.value;
    if (!run) {
        return;
    }

    const fileTypes = {
        text: { extension: 'txt', mime: 'text/plain;charset=utf-8' },
        markdown: { extension: 'md', mime: 'text/markdown;charset=utf-8' },
        json: { extension: 'json', mime: 'application/json;charset=utf-8' },
    };
    const fileType = fileTypes[format];
    if (!fileType) {
        return;
    }

    const content = serializeResults(run, format);
    const blob = new Blob(format === 'json' ? [content] : ['\ufeff', content], { type: fileType.mime });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    const note = (run.note || '').replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_').trim();
    link.download = `skynet-arena-${run.kind}-${note ? `${note}-` : ''}${run.id.slice(0, 8)}.${fileType.extension}`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    message.success(`Results downloaded as ${fileType.extension.toUpperCase()}`);
}

function handleDeckSelection(deck, checked) {
    selectedDecks.value = checked
        ? [...new Set([...selectedDecks.value, deck])]
        : selectedDecks.value.filter((item) => item !== deck);
}

function selectRegressionRetestDecks() {
    allDecks.value = false;
    selectedDecks.value = [...regressionRetestDecks.value];
    deckListExpanded.value = true;
    message.success(`Selected ${selectedDecks.value.length} decks with unusual win rates`);
}

function clearRecentDecks() {
    recentDecks.value = [];
    try {
        localStorage.removeItem('skynet-arena-recent-decks');
        localStorage.removeItem('windbot-arena-recent-decks');
    } catch {
        // Clear recent-use markers immediately in this page.
    }
}

function isAiLevelSelected(aiLevel) {
    const decks = deckOptions.value.filter((option) => option.aiLevel === aiLevel);
    return decks.length > 0 && decks.every((option) => effectiveSelectedDeckSet.value.has(option.value));
}

function isAiLevelIndeterminate(aiLevel) {
    const decks = deckOptions.value.filter((option) => option.aiLevel === aiLevel);
    const selectedCount = decks.filter(
        (option) => effectiveSelectedDeckSet.value.has(option.value),
    ).length;
    return selectedCount > 0 && selectedCount < decks.length;
}

function handleAiLevelSelection(aiLevel, checked) {
    const selected = new Set(effectiveSelectedDeckSet.value);
    for (const option of deckOptions.value) {
        if (option.aiLevel !== aiLevel) {
            continue;
        }
        if (checked) {
            selected.add(option.value);
        } else {
            selected.delete(option.value);
        }
    }
    allDecks.value = false;
    selectedDecks.value = deckOptions.value
        .filter((option) => selected.has(option.value))
        .map((option) => option.value);
}

watch(catalogDeckOptions, (options) => {
    if (!system.value?.configuration?.valid || recentDecksValidated) {
        return;
    }
    const available = new Set(options.map((option) => option.value));
    recentDecks.value = recentDecks.value.filter((deck) => available.has(deck));
    selectedDecks.value = [...recentDecks.value];
    recentDecksValidated = true;
});

watch(experimentKind, () => {
    allDecks.value = false;
    const available = new Set(catalogDeckOptions.value.map((option) => option.value));
    selectedDecks.value = selectedDecks.value.filter((deck) => available.has(deck));
});

onMounted(async () => {
    eventSource = new EventSource('/api/events');
    eventSource.addEventListener('ready', handleRevisionEvent);
    eventSource.addEventListener('heartbeat', handleRevisionEvent);
    eventSource.addEventListener('notification', (event) => {
        const notification = JSON.parse(event.data);
        if ('Notification' in window && Notification.permission === 'granted') {
            try {
                new Notification(notification.title, {
                    body: notification.body,
                    tag: `arena-${notification.runId}`,
                    icon: '/skynet.ico',
                });
            } catch {
                // Some mobile browsers cannot construct notifications; keep refreshing normally.
            }
        }
    });
    eventSource.onerror = () => {
        liveConnected.value = false;
    };
    try {
        await refresh();
    } catch (error) {
        message.error(error.message);
        loading.value = false;
    }
    clockTimer = setInterval(() => { clockNow.value = Date.now(); }, 1000);
});

onBeforeUnmount(() => {
    clearInterval(clockTimer);
    clearInterval(inspectorTimer);
    eventSource?.close();
});
</script>

<template>
    <div class="app-shell">
        <header class="topbar">
            <a class="brand" href="#">
                <span class="brand-mark"><img src="/skynet.ico" alt=""></span>
                <span>
                    <strong>Skynet Arena</strong>
                </span>
            </a>
            <div class="topbar-actions">
                <div class="theme-control" title="Switch light / dark theme">
                    <svg class="theme-icon" :class="{ active: !darkMode }" viewBox="0 0 24 24" aria-hidden="true">
                        <circle cx="12" cy="12" r="4"></circle>
                        <path d="M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.65 17.65l1.42 1.42M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.65 6.35l1.42-1.42"></path>
                    </svg>
                    <n-switch
                        :value="darkMode"
                        size="small"
                        aria-label="Switch light / dark theme"
                        @update:value="emit('update:darkMode', $event)"
                    />
                    <svg class="theme-icon" :class="{ active: darkMode }" viewBox="0 0 24 24" aria-hidden="true">
                        <path d="M20.6 15.7A9 9 0 0 1 8.3 3.4 9 9 0 1 0 20.6 15.7Z"></path>
                    </svg>
                </div>
                <n-button size="small" quaternary @click="openSettings">Settings</n-button>
            </div>
        </header>

        <main>
            <section class="hero">
                <div class="hero-copy">
                    <span class="eyebrow">DUEL EXPERIMENT LAB</span>
                    <h1>Better decisions.<br><em>Measured in duels.</em></h1>
                    <p>Compare policies, challenge opponents, and follow every result. Build evidence for a stronger SKYNET.</p>
                </div>
                <div class="system-readout">
                    <div>
                        <div class="readout-heading">
                            <span>SRVPRO</span>
                            <button type="button" title="View rooms" aria-label="View SRVPro rooms" @click="openInspector('srvpro')">
                                <svg viewBox="0 0 24 24" aria-hidden="true">
                                    <circle cx="11" cy="11" r="6"></circle>
                                    <path d="m16 16 4 4"></path>
                                </svg>
                            </button>
                        </div>
                        <strong :title="selectedSrvpro ? selectedSrvproDisplay : undefined">
                            {{ selectedSrvproDisplay }}
                        </strong>
                    </div>
                    <div>
                        <div class="readout-heading">
                            <span>CANDIDATE AGENT</span>
                            <button type="button" title="View agent output" aria-label="View candidate output" @click="openInspector('current')">
                                <svg viewBox="0 0 24 24" aria-hidden="true">
                                    <circle cx="11" cy="11" r="6"></circle>
                                    <path d="m16 16 4 4"></path>
                                </svg>
                            </button>
                        </div>
                        <strong>{{ formatWindBotEndpoint(system?.windbots.current) }}</strong>
                    </div>
                    <div>
                        <div class="readout-heading">
                            <span>BASELINE AGENT</span>
                            <button type="button" title="View agent output" aria-label="View baseline output" @click="openInspector('old')">
                                <svg viewBox="0 0 24 24" aria-hidden="true">
                                    <circle cx="11" cy="11" r="6"></circle>
                                    <path d="m16 16 4 4"></path>
                                </svg>
                            </button>
                        </div>
                        <strong>{{ formatWindBotEndpoint(system?.windbots.old) }}</strong>
                    </div>
                </div>
            </section>

            <n-spin :show="loading">
                <section class="workspace">
                    <div class="main-column">
                        <article class="panel launch-panel">
                            <n-radio-group
                                v-model:value="experimentKind"
                                class="experiment-tabs"
                                name="experiment-kind"
                                :disabled="!!activeRun"
                            >
                                <n-radio-button value="regression">Regression</n-radio-button>
                                <n-radio-button value="challenge">Challenge</n-radio-button>
                                <n-radio-button value="tag">Tag smoke test</n-radio-button>
                                <n-radio-button value="ranking">Ranking</n-radio-button>
                            </n-radio-group>
                            <n-alert
                                v-if="system && !configurationReady"
                                class="configuration-alert"
                                title="Complete your setup"
                                type="info"
                                :bordered="false"
                            >
                                <ul class="issue-list">
                                    <li v-for="issue in modeConfiguration.issues" :key="issue">{{ issue }}</li>
                                </ul>
                                <n-button size="small" type="primary" ghost @click="openSettings">Open settings</n-button>
                            </n-alert>
                            <div class="panel-heading">
                                <div>
                                    <span class="section-index">{{ runKindIndexes[experimentKind] }}</span>
                                    <div>
                                        <div class="results-title-line">
                                            <h2>{{ runKindLabels[experimentKind] }}</h2>
                                            <n-tag v-if="!activeRun" size="small">Ready</n-tag>
                                            <n-tag
                                                v-else-if="displayedRun?.id !== activeRun.id"
                                                size="small"
                                                type="warning"
                                            >
                                                History
                                            </n-tag>
                                        </div>
                                        <p>{{ runKindDescriptions[experimentKind] }}</p>
                                    </div>
                                </div>
                            </div>

                            <div class="launch-form">
                                <label
                                    v-if="system?.srvpros.length > 1"
                                    class="field field-wide srvpro-field"
                                >
                                    <span>Duel server</span>
                                    <n-select
                                        :value="selectedSrvproId"
                                        :options="srvproOptions"
                                        placeholder="Choose a dedicated duel server"
                                        @update:value="selectSrvpro"
                                    />
                                </label>
                                <div v-if="experimentKind === 'challenge'" class="challenge-target-row">
                                    <label class="field target-deck-field">
                                        <span>Challenger deck</span>
                                        <n-auto-complete
                                            v-model:value="targetDeck"
                                            :disabled="!!activeRun"
                                            :options="targetDeckOptions"
                                            :render-label="(option) => option.displayLabel"
                                            clearable
                                            placeholder="Choose a profile or enter a WindBot executor name"
                                        />
                                    </label>
                                    <div class="field challenger-version-field">
                                        <span>Challenger agent</span>
                                        <n-radio-group
                                            v-model:value="challengerVersion"
                                            :disabled="!!activeRun"
                                            name="challenger-version"
                                            size="small"
                                        >
                                            <n-radio-button value="current">Candidate</n-radio-button>
                                            <n-radio-button value="old">Baseline</n-radio-button>
                                        </n-radio-group>
                                    </div>
                                </div>
                                <div class="field field-wide deck-picker">
                                    <div class="field-heading">
                                        <span>{{ experimentKind === 'challenge' ? 'Opponent decks' : 'Test decks' }}</span>
                                        <div class="field-heading-actions">
                                            <n-button
                                                v-if="experimentKind === 'regression'
                                                    && displayedRun?.kind === 'regression'
                                                    && terminalStatuses.has(displayedRun.status)"
                                                text
                                                type="primary"
                                                size="tiny"
                                                :disabled="!!activeRun || regressionRetestDecks.length === 0"
                                                @click="selectRegressionRetestDecks"
                                            >
                                                Retest outliers
                                            </n-button>
                                            <n-button
                                                text
                                                type="primary"
                                                size="tiny"
                                                :disabled="recentDecks.length === 0"
                                                @click="clearRecentDecks"
                                            >
                                                Clear recent decks
                                            </n-button>
                                            <n-button
                                                text
                                                type="primary"
                                                size="tiny"
                                                :loading="refreshingDecks"
                                                @click="refreshBotConfigs"
                                            >
                                                Refresh catalog
                                            </n-button>
                                        </div>
                                    </div>
                                    <div class="deck-list-box">
                                        <button
                                            type="button"
                                            class="deck-list-toggle"
                                            :aria-expanded="deckListExpanded"
                                            aria-controls="deck-options"
                                            @click="deckListExpanded = !deckListExpanded"
                                        >
                                            <span>
                                                {{ allDecks
                                                    ? `All decks (excluding LV1; ${allDeckCount})`
                                                    : selectedDecks.length
                                                        ? `Selected ${selectedDecks.length} / ${deckOptions.length}`
                                                        : `Choose decks (${deckOptions.length})` }}
                                            </span>
                                            <small>{{ deckListExpanded ? 'Collapse' : 'Expand' }}</small>
                                        </button>
                                        <div v-show="deckListExpanded" id="deck-options" class="deck-options">
                                            <n-checkbox
                                                v-for="option in deckOptions"
                                                :key="option.value"
                                                class="deck-option"
                                                :checked="allDecks
                                                    ? option.aiLevel !== 1
                                                        || (experimentKind === 'challenge'
                                                            && String(targetDeck || '').trim() === option.value)
                                                    : selectedDecks.includes(option.value)"
                                                :disabled="allDecks || !!activeRun"
                                                @update:checked="handleDeckSelection(option.value, $event)"
                                            >
                                                <span class="deck-option-main">
                                                    <strong>{{ option.value }}</strong>
                                                    <small>{{ option.currentLabel }}</small>
                                                </span>
                                                <span class="deck-option-tags">
                                                    <span v-if="recentDeckSet.has(option.value)" class="recent-deck">Recent</span>
                                                    <span v-if="option.aiLevel" class="ai-level">LV{{ option.aiLevel }}</span>
                                                </span>
                                            </n-checkbox>
                                            <span v-if="deckOptions.length === 0" class="deck-options-empty">
                                                No deck profiles available
                                            </span>
                                        </div>
                                    </div>
                                </div>
                                <div class="deck-bulk-controls">
                                    <n-checkbox
                                        v-model:checked="allDecks"
                                        :disabled="!!activeRun"
                                    >
                                        {{ experimentKind === 'regression' ? 'All matching profiles' : 'All candidate decks' }}
                                    </n-checkbox>
                                    <span class="ai-level-controls">
                                        <n-checkbox
                                            v-for="aiLevel in availableAiLevels"
                                            :key="aiLevel ?? 'ungraded'"
                                            :checked="isAiLevelSelected(aiLevel)"
                                            :indeterminate="isAiLevelIndeterminate(aiLevel)"
                                            :disabled="!!activeRun"
                                            @update:checked="handleAiLevelSelection(aiLevel, $event)"
                                        >
                                            {{ aiLevel === null ? 'Unrated' : `LV${aiLevel}` }}
                                        </n-checkbox>
                                    </span>
                                </div>
                                <div class="launch-actions">
                                    <label
                                        v-if="!['ranking', 'tag'].includes(experimentKind)"
                                        class="field games-field"
                                    >
                                        <span>{{ experimentKind === 'challenge' ? 'Duels per opponent' : 'Duels per deck' }}</span>
                                        <n-input-number
                                            v-model:value="configuredGamesPerMatchup"
                                            :disabled="!!activeRun"
                                            :min="1"
                                            :max="10000"
                                            :step="experimentKind === 'challenge' ? 10 : 50"
                                        />
                                    </label>
                                    <n-button
                                        v-if="launchPanelState === 'ready'"
                                        type="primary"
                                        :disabled="!canStart"
                                        :loading="starting"
                                        @click="startRun"
                                    >
                                        Start {{ runKindLabels[experimentKind] }}
                                    </n-button>
                                    <n-button v-else-if="launchPanelState === 'terminal'" disabled>
                                        {{ terminalRunActionLabel }}
                                    </n-button>
                                    <n-dropdown
                                        v-else
                                        trigger="click"
                                        placement="bottom-start"
                                        width="trigger"
                                        style="width: max-content; min-width: 100%"
                                        :options="stopMenuOptions"
                                        :render-option="({ node, option }) => option.description ? h(NTooltip, { placement: 'left', style: { width: '320px', maxWidth: 'calc(100vw - 32px)', lineHeight: '1.6', fontWeight: '400' } }, {
                                            trigger: () => node,
                                            default: () => option.description,
                                        }) : node"
                                        @select="handleStopAction"
                                    >
                                    <n-button-group class="stop-button-group">
                                        <n-tooltip v-if="stopCountdown" placement="left">
                                            <template #trigger>
                                                <n-button type="error" ghost @click.stop="handleStopAction('timer')" style="font-variant-numeric: tabular-nums">
                                                    {{ stopCountdown }}
                                                </n-button>
                                            </template>
                                            Finish active duels when the timer ends. Click to edit, or use the menu to stop immediately.
                                        </n-tooltip>
                                        <n-popconfirm v-else @positive-click="stopRun(false)">
                                            <template #trigger>
                                                <n-button type="error" ghost :loading="stopping" :disabled="activeRun?.status === 'stopping'" @click.stop>Stop experiment</n-button>
                                            </template>
                                            Unfinished duels will be excluded from results. Stop the experiment?
                                        </n-popconfirm>
                                            <n-button class="stop-menu-trigger" type="error" ghost :disabled="stopping || activeRun?.status === 'stopping'" aria-label="Stop options">
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                                                    <path d="m6 9 6 6 6-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                                                </svg>
                                            </n-button>
                                    </n-button-group>
                                    </n-dropdown>
                                </div>
                            </div>
                        </article>

                        <article v-if="displayedRun" class="panel results-panel">
                            <div class="results-header">
                                <div>
                                    <div>
                                        <div class="results-title-line">
                                            <h2>Results</h2>
                                            <n-tag size="small" :type="statusTypes[displayedRun.status]">
                                                {{ statusLabels[displayedRun.status] || displayedRun.status }}
                                            </n-tag>
                                        </div>
                                        <p class="results-meta">
                                            {{ formatHistoryTitle(displayedRun) }}
                                            <button
                                                class="run-note-edit"
                                                type="button"
                                                title="Edit note"
                                                aria-label="Edit experiment note"
                                                @click="editingNoteRun = displayedRun; noteDraft = displayedRun.note || formatHistoryNote(displayedRun)"
                                            >
                                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                                                    <path d="m15 5 4 4M4 20l4-1L20 7a2.8 2.8 0 0 0-4-4L4 15z" />
                                                </svg>
                                            </button>
                                            · <code>{{ displayedRun.id.slice(0, 8).toUpperCase() }}</code>
                                            · {{ displayedRun.config?.srvproName || displayedRun.config?.duelServer || 'SRVPro' }}
                                            · Created {{ formatDate(displayedRun.createdAt) }}
                                            <template v-if="displayedRun.config?.stopAt">
                                                · Stop scheduled for {{ formatDate(displayedRun.config.stopAt) }}
                                            </template>
                                        </p>
                                    </div>
                                </div>
                                <div class="results-actions">
                                    <n-dropdown
                                            trigger="click"
                                            placement="bottom-start"
                                            width="trigger"
                                            style="width: max-content; min-width: 100%"
                                            :options="resultDownloadOptions"
                                            @select="downloadResults"
                                        >
                                        <n-button-group size="small">
                                            <n-button secondary @click.stop="copyResults">Copy text</n-button>
                                            <n-button secondary aria-label="Download results">
                                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                                                    <path d="m6 9 6 6 6-6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                                                </svg>
                                            </n-button>
                                        </n-button-group>
                                    </n-dropdown>
                                </div>
                            </div>

                            <div class="metric-grid">
                                <div class="metric primary-metric">
                                    <span v-if="displayedRun.kind === 'tag'">Test decks</span>
                                    <span v-else>{{ displayedRun.kind !== 'ranking' ? 'Progress' : 'Completed duels' }}</span>
                                    <strong v-if="displayedRun.kind === 'tag'">{{ displayedRun.matchups.length }}</strong>
                                    <strong v-else-if="displayedRun.kind !== 'ranking'" class="progress-value">
                                        {{ displayedRun.observedGames }}<span> / {{ displayedRun.totalGames }}</span>
                                    </strong>
                                    <strong v-else>{{ displayedRun.observedGames }}</strong>
                                    <n-progress
                                        v-if="!['ranking', 'tag'].includes(displayedRun.kind)"
                                        type="line"
                                        :percentage="progress"
                                        :height="5"
                                        :show-indicator="false"
                                        :border-radius="0"
                                    />
                                </div>
                                <div class="metric">
                                    <span>Launched duels</span>
                                    <strong>{{ displayedRun.launchedGames }}</strong>
                                </div>
                                <div class="metric">
                                    <span v-if="displayedRun.kind === 'ranking'">Leading deck</span>
                                    <span
                                        v-else-if="displayedRun.kind === 'tag'"
                                        title="Count errors written to candidate stderr during this experiment"
                                    >
                                        Agent errors
                                    </span>
                                    <span v-else>{{ displayedRun.kind === 'challenge' ? 'Challenger win rate' : 'Candidate win rate' }}</span>
                                    <strong v-if="displayedRun.kind === 'ranking'" class="leader-value">
                                        <span :title="rankingLeader?.label || ''">
                                            {{ rankingLeader?.label || 'Collecting' }}
                                        </span>
                                        <small v-if="rankingLeader">{{ formatPercent(rankingLeader.currentWinRate) }}</small>
                                    </strong>
                                    <strong
                                        v-else-if="displayedRun.kind === 'tag'"
                                        :class="{ 'unavailable-value': !tagWindBotOutputScannable }"
                                    >
                                        {{ tagWindBotOutputScannable ? tagIssueCount : 'Remote output unavailable' }}
                                    </strong>
                                    <strong v-else-if="displayedRun.kind === 'challenge'" class="challenge-rate-value">
                                        <span :title="displayedRun.config?.targetDeck || ''">
                                            {{ displayedRun.config?.targetDeck || '—' }}
                                            · {{ displayedRun.config?.challengerVersion === 'old' ? 'Baseline' : 'Candidate' }}
                                        </span>
                                        <small>
                                            {{ formatPercent(currentWinRate) }}
                                            <template v-if="displayedRun.challengeTargetFlee > 0">
                                                · Disconnects {{ displayedRun.challengeTargetFlee }}
                                            </template>
                                        </small>
                                    </strong>
                                    <strong v-else>{{ formatPercent(currentWinRate) }}</strong>
                                </div>
                                <div class="metric">
                                    <span>Elapsed time</span>
                                    <strong class="time-value">{{ formatDuration(elapsedMilliseconds) }}</strong>
                                </div>
                                <div class="metric">
                                    <span>
                                        {{ terminalStatuses.has(displayedRun.status)
                                            ? 'Finished at'
                                            : !['ranking', 'tag'].includes(displayedRun.kind) ? 'Estimated finish' : 'Run mode' }}
                                    </span>
                                    <strong class="time-value">
                                        {{ terminalStatuses.has(displayedRun.status)
                                            ? formatDate(displayedRun.finishedAt)
                                            : !['ranking', 'tag'].includes(displayedRun.kind)
                                                ? estimatedCompletionAt ? formatDate(estimatedCompletionAt) : 'Estimating'
                                                : 'Manual stop' }}
                                    </strong>
                                </div>
                            </div>

                            <div v-if="displayedRun.kind === 'regression'" class="table-wrap">
                                <table class="result-table">
                                    <thead>
                                        <tr>
                                            <th>Deck</th>
                                            <th>Results collected</th>
                                            <th>W / L / disconnects</th>
                                            <th>Candidate win rate</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="matchup in regressionRows" :key="matchup.id">
                                            <td class="deck-cell">
                                                <strong>{{ matchup.label }}</strong>
                                                <small>
                                                    <span v-if="matchup.aiLevel">LV{{ matchup.aiLevel }} · </span>
                                                    {{ matchup.competitors[0]?.botLabel }}
                                                </small>
                                            </td>
                                            <td>
                                                <span class="progress-number">
                                                    {{ matchup.observedGames }} / {{ matchup.targetGames }}
                                                </span>
                                            </td>
                                            <td class="score current-score">
                                                {{ matchup.competitors[0]?.win || 0 }} /
                                                {{ matchup.competitors[0]?.lose || 0 }} /
                                                <span
                                                    class="flee-count"
                                                    :class="{ 'is-nonzero': matchup.competitors[0]?.flee > 0 }"
                                                >
                                                    {{ matchup.competitors[0]?.flee || 0 }}
                                                </span>
                                            </td>
                                            <td
                                                class="deck-win-rate"
                                                :class="{
                                                    'is-significant-win': matchup.observedGames >= 100
                                                        && matchup.currentWinRate > 0.55,
                                                    'is-significant-loss': matchup.observedGames >= 100
                                                        && matchup.currentWinRate < 0.45,
                                                }"
                                            >
                                                {{ formatPercent(matchup.currentWinRate) }}
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <div v-else-if="displayedRun.kind === 'challenge'" class="table-wrap">
                                <table class="result-table">
                                    <thead>
                                        <tr>
                                            <th>Opponent decks</th>
                                            <th>Results collected</th>
                                            <th>Challenger W / L</th>
                                            <th>Opponent disconnects</th>
                                            <th>Challenger win rate</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="matchup in challengeRows" :key="matchup.id">
                                            <td class="deck-cell">
                                                <strong>{{ matchup.label }}</strong>
                                                <small>
                                                    <span v-if="matchup.aiLevel">LV{{ matchup.aiLevel }} · </span>
                                                    {{ matchup.competitors[1]?.botLabel }}
                                                </small>
                                            </td>
                                            <td>
                                                <span class="progress-number">
                                                    {{ matchup.observedGames }} / {{ matchup.targetGames }}
                                                </span>
                                            </td>
                                            <td class="score current-score">
                                                {{ matchup.competitors[0]?.win || 0 }} /
                                                {{ matchup.competitors[0]?.lose || 0 }}
                                            </td>
                                            <td class="score current-score">
                                                <span
                                                    class="flee-count"
                                                    :class="{ 'is-nonzero': matchup.competitors[1]?.flee > 0 }"
                                                >
                                                    {{ matchup.competitors[1]?.flee || 0 }}
                                                </span>
                                            </td>
                                            <td class="deck-win-rate">
                                                {{ formatPercent(matchup.currentWinRate) }}
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <div v-else-if="displayedRun.kind === 'tag'" class="table-wrap">
                                <table class="result-table">
                                    <thead>
                                        <tr>
                                            <th>Test decks</th>
                                            <th>Duels played</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="entry in tagRows" :key="entry.id">
                                            <td class="deck-cell">
                                                <strong>{{ entry.label }}</strong>
                                                <small>
                                                    <span v-if="entry.aiLevel">LV{{ entry.aiLevel }} · </span>
                                                    {{ entry.competitors[0]?.botLabel }}
                                                </small>
                                            </td>
                                            <td><span class="progress-number">{{ entry.launchedGames }}</span></td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <div v-else class="table-wrap">
                                <table class="result-table ranking-table">
                                    <thead>
                                        <tr>
                                            <th>Rank</th>
                                            <th>Deck</th>
                                            <th>Collected / launched</th>
                                            <th>W / L / disconnects</th>
                                            <th>Win rate</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="(entry, index) in rankingRows" :key="entry.id">
                                            <td class="ranking-position">{{ index + 1 }}</td>
                                            <td class="deck-cell">
                                                <strong>{{ entry.label }}</strong>
                                                <small>
                                                    <span v-if="entry.aiLevel">LV{{ entry.aiLevel }} · </span>
                                                    {{ entry.competitors[0]?.botLabel }}
                                                </small>
                                            </td>
                                            <td>
                                                <span class="progress-number">
                                                    {{ entry.observedGames }} / {{ entry.launchedGames }}
                                                </span>
                                            </td>
                                            <td class="score current-score">
                                                {{ entry.competitors[0]?.win || 0 }} /
                                                {{ entry.competitors[0]?.lose || 0 }} /
                                                <span
                                                    class="flee-count"
                                                    :class="{ 'is-nonzero': entry.competitors[0]?.flee > 0 }"
                                                >
                                                    {{ entry.competitors[0]?.flee || 0 }}
                                                </span>
                                            </td>
                                            <td class="deck-win-rate">{{ formatPercent(entry.currentWinRate) }}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </article>

                        <article v-else class="panel empty-panel">
                            <n-empty description="No experiments yet. Configure your agents and start a comparison." />
                        </article>
                    </div>

                    <aside class="side-column">
                        <article class="panel history-panel">
                            <div class="aside-heading">
                                <div>
                                    <h2>History</h2>
                                </div>
                                <span>{{ historyTotal }}</span>
                            </div>
                            <div v-if="runs.length" class="history-list">
                                <div
                                    v-for="run in runs"
                                    :key="run.id"
                                    class="history-entry"
                                    :class="{ active: displayedRun?.id === run.id }"
                                >
                                    <button class="history-item" @click="selectRun(run.id)">
                                        <span class="history-status" :class="`status-${run.status}`"></span>
                                        <span class="history-main">
                                            <strong :title="formatHistoryTitle(run)">
                                                {{ formatHistoryTitle(run) }}
                                            </strong>
                                            <small>
                                                {{ formatDate(run.createdAt) }}
                                                · {{ formatDuration(getRunElapsedMilliseconds(run)) }}
                                                · {{ statusLabels[run.status] }}
                                            </small>
                                        </span>
                                    </button>
                                    <n-popconfirm
                                        v-if="terminalStatuses.has(run.status)"
                                        :disabled="!shouldConfirmRunDeletion(run)"
                                        @positive-click="deleteRun(run.id)"
                                    >
                                        <template #trigger>
                                            <button
                                                class="history-delete"
                                                type="button"
                                                title="Delete experiment history"
                                                :aria-label="`Delete experiment history ${formatHistoryTitle(run)}`"
                                                :disabled="deletingRunId === run.id"
                                                @click.stop="!shouldConfirmRunDeletion(run) && deleteRun(run.id)"
                                            >
                                                ×
                                            </button>
                                        </template>
                                        Delete this experiment history? This cannot be undone.
                                    </n-popconfirm>
                                </div>
                                <div
                                    v-for="index in historyPlaceholderCount"
                                    :key="`placeholder-${index}`"
                                    class="history-entry history-entry-placeholder"
                                    aria-hidden="true"
                                >
                                    <div class="history-item">
                                        <span class="history-status"></span>
                                        <span class="history-main">
                                            <strong>&nbsp;</strong>
                                            <small>&nbsp;</small>
                                        </span>
                                    </div>
                                </div>
                            </div>
                            <div v-if="historyTotal > historyPageSize" class="history-pagination">
                                <n-button
                                    size="tiny"
                                    secondary
                                    :disabled="historyPage === 1"
                                    @click="selectHistoryPage(historyPage - 1)"
                                >
                                    Previous
                                </n-button>
                                <span>{{ historyPage }} / {{ historyPageCount }}</span>
                                <n-button
                                    size="tiny"
                                    secondary
                                    :disabled="historyPage === historyPageCount"
                                    @click="selectHistoryPage(historyPage + 1)"
                                >
                                    Next
                                </n-button>
                            </div>
                            <n-empty v-if="!runs.length" size="small" description="No previous experiments" />
                        </article>

                        <article v-if="displayedRun" class="panel event-panel">
                            <div class="aside-heading">
                                <div>
                                    <h2>Activity</h2>
                                </div>
                            </div>
                            <ol class="event-list">
                                <li v-for="event in [...displayedRun.events].reverse().slice(0, 8)" :key="event.id">
                                    <time>{{ formatDate(event.at, true) }}</time>
                                    <span :class="`event-${event.level}`">{{ event.message }}</span>
                                </li>
                            </ol>
                        </article>

                        <article class="future-panel">
                            <span>EXTENSIBLE CORE</span>
                            <strong>A repeatable path to improvement</strong>
                            <p>Choose a deck, a policy, and an opponent. Save results before promoting a new model.</p>
                        </article>
                    </aside>
                </section>
            </n-spin>
        </main>

        <footer>
            <span>SKYNET ARENA / MDPRO4 AI SYSTEMS</span>
            <div class="footer-status">
                <span>
                    {{ hasActiveRuns ? `${activeRuns.length} active experiments` : 'Selected experiment' }}:
                    {{ formatDate(displayedRun?.latestRankAt, true) }}
                </span>
                <div class="live-state" :class="{ connected: liveConnected }" role="status">
                    <span class="live-dot"></span>
                    {{ liveConnected ? 'Live updates connected' : 'Reconnecting' }}
                </div>
            </div>
        </footer>

        <n-modal
            :show="!!timerRunId"
            preset="dialog"
            title="Stop timer"
            positive-text="Start timer"
            negative-text="Cancel"
            :positive-button-props="{ loading: timerSaving, disabled: timerSaving || !Number.isInteger(stopAfterMinutes) || stopAfterMinutes < 1 || stopAfterMinutes > 10080 }"
            :mask-closable="!timerSaving"
            :closable="!timerSaving"
            :close-on-esc="!timerSaving"
            :negative-button-props="{ disabled: timerSaving }"
            @positive-click="saveScheduledStop"
            @update:show="!$event && !timerSaving && (timerRunId = '')"
        >
            <p>Start a countdown now. When it ends, stop creating rooms and wait for active duels before collecting results.</p>
            <n-input-number v-model:value="stopAfterMinutes" :min="1" :max="10080" :precision="0" :disabled="timerSaving" aria-label="Stop timer duration">
                <template #suffix>minutes</template>
            </n-input-number>
        </n-modal>
        <n-modal
            :show="!!stopConfirmationRunId"
            preset="dialog"
            title="Stop immediately"
            content="Unfinished duels will be excluded from results. Stop immediately?"
            positive-text="Confirm"
            negative-text="Cancel"
            @positive-click="stopRun(false, stopConfirmationRunId)"
            @update:show="!$event && (stopConfirmationRunId = '')"
        />
        <n-modal
            :show="!!editingNoteRun"
            preset="card"
            title="Edit experiment note"
            style="width: min(480px, calc(100vw - 32px))"
            :mask-closable="!savingNote"
            :closable="!savingNote"
            :close-on-esc="!savingNote"
            @update:show="!$event && (editingNoteRun = null)"
        >
            <form style="display: flex; flex-direction: column; align-items: flex-start; gap: 16px" @submit.prevent="saveRunNote">
                <n-input
                    v-model:value="noteDraft"
                    aria-label="Experiment note"
                    placeholder="Leave empty to use the automatic description"
                    :maxlength="200"
                    :disabled="savingNote"
                    clearable
                    show-count
                />
                <n-button type="primary" attr-type="submit" :loading="savingNote">Save</n-button>
            </form>
        </n-modal>

        <settings-modal
            v-model:show="settingsOpen"
            :disabled="hasActiveRuns"
            :record="settingsRecord"
            :saving="savingSettings"
            @save="saveSettings"
        />

        <n-modal
            :show="inspectorOpen"
            :mask-closable="false"
            @update:show="$event ? null : closeInspector()"
        >
            <n-card
                class="inspector-card"
                :title="inspectorTitle"
                :bordered="false"
                role="dialog"
                aria-modal="true"
            >
                <template #header-extra>
                    <div class="inspector-header-actions">
                        <label v-if="inspectorKind === 'srvpro'" class="halfwaywatch-control">
                            <span>Allow spectators</span>
                            <n-switch
                                :value="inspectorHalfwayWatchEnabled === true"
                                :disabled="inspectorHalfwayWatchEnabled === null"
                                :loading="updatingHalfwayWatch"
                                size="small"
                                @update:value="updateHalfwayWatch"
                            />
                        </label>
                        <n-button size="small" secondary @click="closeInspector">Close</n-button>
                    </div>
                </template>
                <n-alert v-if="inspectorError" type="error" :bordered="false">
                    {{ inspectorError }}
                </n-alert>
                <n-spin :show="inspectorLoading">
                    <template v-if="inspectorKind === 'srvpro'">
                        <div v-if="inspectorRooms.length > 0" class="room-table-wrap">
                            <table class="room-table">
                                <thead>
                                    <tr>
                                        <th>#</th>
                                        <th>Room</th>
                                        <th>Players (team A)</th>
                                        <th>Players (team B)</th>
                                        <th>Status</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr v-for="room in inspectorRooms" :key="room.id">
                                        <td>{{ room.id }}</td>
                                        <td>
                                            <button
                                                v-if="inspectorHalfwayWatchEnabled"
                                                type="button"
                                                class="room-name-button"
                                                title="Copy room name"
                                                @click="copyRoomName(room.name)"
                                            >
                                                {{ room.name }}
                                            </button>
                                            <span v-else class="room-name-text">{{ room.name }}</span>
                                        </td>
                                        <td>
                                            <div v-if="room.mode === 2" class="room-team-members">
                                                <span>{{ formatRoomPlayer(getRoomPlayer(room, 0), false) }}</span>
                                                <span>{{ getRoomPlayer(room, 1)?.name || '—' }}</span>
                                            </div>
                                            <span v-else>{{ formatRoomPlayer(getRoomPlayer(room, 0)) }}</span>
                                        </td>
                                        <td>
                                            <div v-if="room.mode === 2" class="room-team-members">
                                                <span>{{ formatRoomPlayer(getRoomPlayer(room, 2), false) }}</span>
                                                <span>{{ getRoomPlayer(room, 3)?.name || '—' }}</span>
                                            </div>
                                            <span v-else>{{ formatRoomPlayer(getRoomPlayer(room, 1)) }}</span>
                                        </td>
                                        <td>{{ room.status || '—' }}</td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                        <n-empty v-else-if="!inspectorLoading && !inspectorError" description="No active rooms" />
                    </template>
                    <template v-else>
                        <n-alert
                            v-if="inspectorOutput && !inspectorOutput.available"
                            type="warning"
                            :bordered="false"
                        >
                            Remote WindBot output is managed on its own host and is unavailable here.
                        </n-alert>
                        <pre v-else-if="inspectorOutput?.output" class="command-output">{{ inspectorOutput.output }}</pre>
                        <n-empty
                            v-else-if="!inspectorLoading && !inspectorError"
                            description="No local agent output yet"
                        />
                    </template>
                </n-spin>
                <template #footer>
                    <div class="inspector-footer">
                        <span>Refreshes every 2 seconds</span>
                        <n-button size="small" :loading="inspectorLoading" @click="loadInspector()">Refresh now</n-button>
                    </div>
                </template>
            </n-card>
        </n-modal>
    </div>
</template>
