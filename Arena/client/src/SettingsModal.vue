<script setup>
import { ref, watch } from 'vue';
import {
    NAlert,
    NButton,
    NCard,
    NCheckbox,
    NForm,
    NFormItem,
    NInput,
    NInputNumber,
    NModal,
    NRadioButton,
    NRadioGroup,
    NSelect,
    NTabPane,
    NTabs,
} from 'naive-ui';

const props = defineProps({
    disabled: Boolean,
    record: { type: Object, default: null },
    saving: Boolean,
    show: Boolean,
});
const emit = defineEmits(['save', 'update:show']);
const draft = ref(null);
const cancelButton = ref(null);
const notificationTestResult = ref('');
const testingNotification = ref(false);
const webhookHeadersExample = JSON.stringify({ Authorization: 'Bearer your token' });
const webhookExample = JSON.stringify({
    title: '{{title}}',
    body: '{{body}}',
}, null, 2);
const windbotLabels = {
    current: 'Candidate agent',
    old: 'Baseline agent',
};
const policyOptions = [
    { label: 'SKYNET · general policy', value: 'skynet' },
    { label: 'Expert · OldSchool baseline', value: 'expert-oldschool' },
    { label: 'Expert · Swordsoul baseline', value: 'expert-swordsoul' },
];

function addProfile(name) {
    draft.value.windbots[name].skynet.profiles.push({
        id: '', label: '', deckPath: '', agent: 'skynet', modelPath: '',
    });
}

function cloneSettings(value) {
    return JSON.parse(JSON.stringify(value));
}

watch(() => [props.show, props.record], () => {
    if (props.show && props.record) {
        draft.value = cloneSettings(props.record.settings);
    }
}, { immediate: true });

function close() {
    emit('update:show', false);
}

function save() {
    emit('save', cloneSettings(draft.value));
}

async function testNotification() {
    if (testingNotification.value) return;
    testingNotification.value = true;
    notificationTestResult.value = '';
    try {
        if (draft.value.notifications.mode === 'frontend') {
            if (!window.isSecureContext || !('Notification' in window)) {
                notificationTestResult.value = 'Browser notifications require a supported browser and HTTPS or localhost.';
                return;
            }
            const permission = Notification.permission === 'default'
                ? await Notification.requestPermission() : Notification.permission;
            if (permission !== 'granted') {
                notificationTestResult.value = 'Allow notifications in browser site settings, then try again.';
                return;
            }
            try {
                new Notification('Skynet Arena · Test notification', {
                    body: 'This is a Skynet Arena test notification.', tag: 'arena-notification-test', icon: '/skynet.ico',
                });
                notificationTestResult.value = 'Notification requested. Check your device notification center.';
            } catch {
                notificationTestResult.value = 'This browser cannot display notifications.';
            }
        } else if (draft.value.notifications.mode === 'webhook') {
            const response = await fetch('/api/notifications/test', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(draft.value.notifications),
            });
            const result = await response.json();
            if (!response.ok) throw new Error(result.error || 'Could not send test notification');
            notificationTestResult.value = result.message;
        }
    } catch (error) {
        notificationTestResult.value = error.message || 'Notification test failed';
    } finally {
        testingNotification.value = false;
    }
}

function addSrvpro() {
    let id;
    do {
        const parts = new Uint32Array(4);
        if (typeof globalThis.crypto?.getRandomValues === 'function') {
            globalThis.crypto.getRandomValues(parts);
        } else {
            for (let index = 0; index < parts.length; index++) {
                parts[index] = Math.floor(Math.random() * 0x100000000);
            }
        }
        const token = Array.from(
            parts,
            (part) => part.toString(36).padStart(7, '0'),
        ).join('');
        id = `srvpro-${token}`;
    } while (draft.value.srvpros.some((srvpro) => srvpro.id === id));
    draft.value.srvpros.push({
        duelPort: 7911,
        host: '',
        id,
        maxRooms: 100,
        name: `SRVPro ${draft.value.srvpros.length + 1}`,
        password: '',
        roomsPerSecond: 1,
        statusPort: 7922,
        username: '',
    });
}

function removeSrvpro(index) {
    if (draft.value.srvpros.length > 1) {
        draft.value.srvpros.splice(index, 1);
    }
}
</script>

<template>
    <n-modal
        v-if="draft"
        :show="show"
        :mask-closable="false"
        :auto-focus="false"
        @after-enter="cancelButton?.$el.focus({ preventScroll: true })"
        @update:show="emit('update:show', $event)"
    >
        <n-card
            class="settings-card"
            :content-style="{ minHeight: '0', display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '0' }"
            :segmented="{ action: true }"
            title="Skynet Arena settings"
            :bordered="false"
            role="dialog"
            aria-modal="true"
        >
            <n-tabs
                v-if="show"
                type="line"
                class="settings-tabs"
                default-value="windbots"
                :pane-style="{ flex: '1', minHeight: '0', overflowY: 'auto', scrollbarGutter: 'stable', paddingLeft: 'var(--n-padding-left)', paddingRight: 'var(--n-padding-left)', paddingBottom: '22px' }"
            >
                <n-tab-pane name="notifications" tab="Notifications">
                    <n-form label-placement="top" class="settings-form" :show-feedback="false">
                        <n-alert type="info" :bordered="false">
                            Notify on completion, interruption, or failure. Browser notifications need this page open. Webhooks are sent by the server.
                        </n-alert>
                        <n-form-item label="Notification mode">
                            <n-radio-group v-model:value="draft.notifications.mode">
                                <n-radio-button value="off">Close</n-radio-button>
                                <n-radio-button value="frontend">Browser notifications</n-radio-button>
                                <n-radio-button value="webhook">Webhook</n-radio-button>
                            </n-radio-group>
                        </n-form-item>
                        <template v-if="draft.notifications.mode === 'frontend'">
                            <n-form-item label="Browser notifications">
                                <div class="notification-test-row">
                                    <n-button :loading="testingNotification" @click="testNotification">Test notification</n-button>
                                    <n-alert v-if="notificationTestResult" type="info" :bordered="false">{{ notificationTestResult }}</n-alert>
                                </div>
                            </n-form-item>
                            <p>Test once to request browser permission. Keep this page open to receive notifications.</p>
                        </template>
                        <template v-if="draft.notifications.mode === 'webhook'">
                            <n-form-item label="HTTP method">
                                <n-radio-group v-model:value="draft.notifications.webhookMethod">
                                    <n-radio-button value="POST">POST</n-radio-button>
                                    <n-radio-button value="GET">GET</n-radio-button>
                                </n-radio-group>
                            </n-form-item>
                            <n-form-item label="Webhook URL template">
                                <n-input v-model:value="draft.notifications.webhookUrl"
                                    placeholder="https://example.com/notify?access_token=…" />
                            </n-form-item>
                            <n-form-item v-if="draft.notifications.webhookMethod === 'POST'" label="JSON body template">
                                <n-input v-model:value="draft.notifications.webhookTemplate" type="textarea" :autosize="{ minRows: 6, maxRows: 16 }"
                                    :placeholder="webhookExample" />
                            </n-form-item>
                            <p v-if="draft.notifications.webhookMethod === 'POST'">POST supports nested JSON and fixed fields such as accessToken. Its body template must not be empty.</p>
                            <p v-pre>Template variables: {{title}}, {{body}}, {{runId}}, {{status}}, {{finishedAt}}, {{srvproId}}, {{note}}.</p>
                            <p v-pre>{{title}} identifies Skynet Arena and the final status. {{body}} contains the experiment note (or its ID) and SRVPro instance ID.</p>
                            <n-form-item label="HTTP headers (optional)">
                                <n-input v-model:value="draft.notifications.webhookHeaders" type="textarea" :autosize="{ minRows: 2, maxRows: 6 }"
                                    :placeholder="webhookHeadersExample" />
                            </n-form-item>
                            <p>Enter HTTP headers as JSON, or leave empty for defaults. <span v-if="draft.notifications.webhookMethod === 'POST'">POST always uses application/json.</span></p>
                            <div class="notification-test-row">
                                <n-button :loading="testingNotification" @click="testNotification">Test notification</n-button>
                                <n-alert v-if="notificationTestResult" type="info" :bordered="false">{{ notificationTestResult }}</n-alert>
                            </div>
                            <p>Send a test with the values in this form; saving is not required.</p>
                        </template>
                    </n-form>
                </n-tab-pane>
                <n-tab-pane name="srvpro" tab="SRVPro">
                    <n-alert type="info" :bordered="false">
                        Install and run a dedicated arena-srvpro server. Each experiment exclusively uses its selected instance. Separate instances can run concurrently.
                    </n-alert>
                    <div class="srvpro-settings">
                        <section
                            v-for="(srvpro, index) in draft.srvpros"
                            :key="srvpro.id"
                            class="srvpro-instance"
                        >
                            <div class="instance-heading">
                                <div>
                                    <span>INSTANCE {{ String(index + 1).padStart(2, '0') }}</span>
                                    <h3>{{ srvpro.name || 'Unnamed SRVPro' }}</h3>
                                </div>
                                <n-button
                                    size="small"
                                    tertiary
                                    type="error"
                                    :disabled="draft.srvpros.length === 1"
                                    @click="removeSrvpro(index)"
                                >
                                    Remove instance
                                </n-button>
                            </div>
                            <n-form label-placement="top" :show-feedback="false">
                                <div class="settings-grid">
                                    <n-form-item label="Instance name">
                                        <n-input v-model:value="srvpro.name" placeholder="For example: local, NAS-2" />
                                    </n-form-item>
                                    <n-form-item label="Host">
                                        <n-input v-model:value="srvpro.host" placeholder="srvpro.lan or 192.168.1.20" />
                                    </n-form-item>
                                    <n-form-item label="Duel port">
                                        <n-input-number v-model:value="srvpro.duelPort" :min="1" :max="65535" />
                                    </n-form-item>
                                    <n-form-item label="Management port">
                                        <n-input-number v-model:value="srvpro.statusPort" :min="1" :max="65535" />
                                    </n-form-item>
                                    <n-form-item label="Management username">
                                        <n-input v-model:value="srvpro.username" />
                                    </n-form-item>
                                    <n-form-item label="Management password">
                                        <n-input
                                            v-model:value="srvpro.password"
                                            type="password"
                                            show-password-on="click"
                                            :placeholder="record.secretStatus.srvpros[srvpro.id]?.passwordConfigured ? 'Leave empty to keep the saved password' : 'Not configured'"
                                        />
                                    </n-form-item>
                                    <n-form-item label="Maximum rooms">
                                        <n-input-number v-model:value="srvpro.maxRooms" :min="1" />
                                    </n-form-item>
                                    <n-form-item label="Rooms created per second">
                                        <n-input-number
                                            v-model:value="srvpro.roomsPerSecond"
                                            :min="1"
                                            :max="100"
                                        />
                                    </n-form-item>
                                </div>
                            </n-form>
                        </section>
                        <n-button dashed block @click="addSrvpro">Add SRVPro instance</n-button>
                    </div>
                </n-tab-pane>

                <n-tab-pane name="windbots" tab="Agents">
                    <n-alert type="info" :bordered="false">
                        Configure a candidate first. Add a baseline for regression comparisons. Native SKYNET launches one owned process per match participant.
                    </n-alert>
                    <div class="windbot-settings">
                        <section v-for="name in ['current', 'old']" :key="name" class="windbot-instance">
                            <div class="instance-heading">
                                <div>
                                    <span>{{ name === 'current' ? 'CANDIDATE' : 'BASELINE' }}</span>
                                    <h3>{{ windbotLabels[name] }}</h3>
                                </div>
                                <n-radio-group v-model:value="draft.windbots[name].mode" size="small">
                                    <n-radio-button value="skynet">SKYNET</n-radio-button>
                                    <n-radio-button value="local">Local WindBot</n-radio-button>
                                    <n-radio-button value="remote">Remote WindBot</n-radio-button>
                                </n-radio-group>
                            </div>

                            <n-form label-placement="top" :show-feedback="false">
                                <div class="settings-grid">
                                    <template v-if="draft.windbots[name].mode === 'skynet'">
                                        <n-form-item label="SKYNET executable" class="span-2">
                                            <n-input v-model:value="draft.windbots[name].skynet.executablePath" placeholder="Absolute path to SKYNET.exe" />
                                        </n-form-item>
                                        <n-form-item label="Card database" class="span-2">
                                            <n-input v-model:value="draft.windbots[name].skynet.databasePath" placeholder="Absolute path to cards.cdb" />
                                        </n-form-item>
                                        <n-form-item label="Protocol version">
                                            <n-input-number v-model:value="draft.windbots[name].skynet.version" :min="1" :max="65535" :precision="0" />
                                        </n-form-item>
                                        <n-form-item label="Maximum processes">
                                            <n-input-number v-model:value="draft.windbots[name].skynet.maxProcesses" :min="2" :max="32" :precision="0" />
                                        </n-form-item>
                                        <n-form-item label="Match timeout (seconds)">
                                            <n-input-number v-model:value="draft.windbots[name].skynet.duelTimeoutSeconds" :min="10" :max="3600" :precision="0" />
                                        </n-form-item>
                                        <n-form-item label="Decision explanations" class="span-2">
                                            <div class="profile-help">
                                                <n-checkbox v-model:checked="draft.windbots[name].skynet.explainDecisions">
                                                    Explain choices, alternatives and rejected actions
                                                </n-checkbox>
                                                <p>Off by default. Applies to the general policy in new duels after saving. Open <strong>{{ name === 'current' ? 'Candidate output' : 'Baseline output' }}</strong> at the top of Arena to read the explanations. Expert executors do not provide these scores.</p>
                                            </div>
                                        </n-form-item>
                                        <div class="span-2 profile-help">
                                            <h4>Deck profiles</h4>
                                            <p>Give the same matchup ID to matching candidate and baseline decks. Choose a policy for each deck; model files are optional frozen SKYNET weights. Evaluation never trains a model. Participants using the same executable share its process limit.</p>
                                        </div>
                                        <section v-for="(profile, index) in draft.windbots[name].skynet.profiles" :key="index" class="span-2 deck-profile">
                                            <div class="profile-heading">
                                                <strong>Profile {{ index + 1 }}</strong>
                                                <n-button size="small" quaternary type="error" @click="draft.windbots[name].skynet.profiles.splice(index, 1)">Remove</n-button>
                                            </div>
                                            <div class="settings-grid">
                                                <n-form-item label="Matchup ID">
                                                    <n-input v-model:value="profile.id" :maxlength="64" placeholder="oldschool" />
                                                </n-form-item>
                                                <n-form-item label="Display name">
                                                    <n-input v-model:value="profile.label" :maxlength="17" show-count placeholder="OldSchool" />
                                                </n-form-item>
                                                <n-form-item label="Deck file" class="span-2">
                                                    <n-input v-model:value="profile.deckPath" placeholder="Absolute path to a .ydk file" />
                                                </n-form-item>
                                                <n-form-item label="Decision policy" class="span-2">
                                                    <n-select v-model:value="profile.agent" :options="policyOptions" @update:value="profile.modelPath = ''" />
                                                </n-form-item>
                                                <n-form-item v-if="profile.agent === 'skynet'" label="Model file (optional)" class="span-2">
                                                    <n-input v-model:value="profile.modelPath" placeholder="Leave empty for built-in weights" />
                                                </n-form-item>
                                            </div>
                                        </section>
                                        <n-button class="span-2" dashed block @click="addProfile(name)">Add deck profile</n-button>
                                    </template>
                                    <template v-else-if="draft.windbots[name].mode === 'local'">
                                        <n-form-item label="WindBot runtime directory" class="span-2">
                                            <n-input
                                                v-model:value="draft.windbots[name].runtimeDir"
                                                placeholder="Directory containing WindBot.exe, Decks, and Dialogs"
                                            />
                                        </n-form-item>
                                        <n-form-item label="bot.conf path" class="span-2">
                                            <n-input v-model:value="draft.windbots[name].botConfPath" />
                                        </n-form-item>
                                    </template>
                                    <template v-else>
                                        <n-form-item label="Remote host" class="span-2">
                                            <n-input
                                                v-model:value="draft.windbots[name].host"
                                                placeholder="Remote WindBot hostname or IP"
                                            />
                                        </n-form-item>
                                        <n-form-item label="Remote bot.conf URL (optional)" class="span-2">
                                            <n-input
                                                v-model:value="draft.windbots[name].botConfUrl"
                                                placeholder="https://windbot.example.com/bot.conf"
                                            />
                                        </n-form-item>
                                        <n-form-item label="Remote bot.conf (pasted or fetched)" class="span-2">
                                            <n-input
                                                v-model:value="draft.windbots[name].botConfText"
                                                type="textarea"
                                                :autosize="{ minRows: 8, maxRows: 16 }"
                                                placeholder="Paste a catalog, or set a URL to replace this content when saving."
                                            />
                                        </n-form-item>
                                    </template>
                                    <n-form-item v-if="draft.windbots[name].mode !== 'skynet'" label="HTTP port">
                                        <n-input-number v-model:value="draft.windbots[name].port" :min="1" :max="65535" />
                                    </n-form-item>
                                </div>
                            </n-form>

                            <n-alert
                                v-if="draft.windbots[name].mode === 'remote'"
                                type="warning"
                                :bordered="false"
                            >
                                Start the remote WindBot in ServerMode before testing. Saving fetches a configured bot.conf URL; Refresh catalog fetches it again.
                            </n-alert>
                        </section>
                    </div>
                </n-tab-pane>

            </n-tabs>

            <template #action>
                <div class="settings-footer">
                    <div v-if="disabled" class="settings-footer-alerts">
                        <n-alert v-if="disabled" type="warning" :bordered="false">
                            Finish active experiments before saving settings.
                        </n-alert>
                    </div>
                    <div class="settings-actions">
                        <n-button ref="cancelButton" @click="close">Cancel</n-button>
                        <n-button type="primary" :disabled="disabled" :loading="saving" @click="save">Save settings</n-button>
                    </div>
                </div>
            </template>
        </n-card>
    </n-modal>
</template>

<style scoped>
.settings-card {
    width: min(920px, calc(100vw - 32px));
    height: calc(100vh - 48px);
    height: calc(100dvh - 48px);
    overflow: hidden;
}

.settings-tabs {
    flex: 1;
    min-height: 0;
}

.settings-tabs :deep(.n-tabs-nav) {
    flex-shrink: 0;
    position: relative;
    margin-inline: var(--n-padding-left);
}

.settings-tabs :deep(.n-tabs-nav::before),
.settings-tabs :deep(.n-tabs-nav::after) {
    content: '';
    position: absolute;
    bottom: 0;
    width: var(--n-padding-left);
    height: 1px;
    background: var(--n-tab-border-color);
    pointer-events: none;
}

.settings-tabs :deep(.n-tabs-nav::before) {
    right: 100%;
}

.settings-tabs :deep(.n-tabs-nav::after) {
    left: 100%;
}

.settings-card :deep(.n-alert) {
    margin-bottom: 22px;
}

.settings-card :deep(.n-form-item) {
    margin-bottom: 12px;
}

.settings-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0 18px;
}

.span-2 {
    grid-column: span 2;
}

.profile-help p {
    color: var(--n-text-color);
    line-height: 1.6;
    opacity: 0.75;
}

.deck-profile {
    margin-bottom: 16px;
    padding: 16px;
    border: 1px solid var(--n-border-color);
    border-radius: 10px;
}

.profile-heading {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 12px;
}

.windbot-settings {
    display: grid;
    gap: 18px;
}

.windbot-instance,
.srvpro-instance {
    padding: 20px;
    border: 1px solid #1d333c;
    border-radius: 12px;
    background: #11151d;
}

.srvpro-settings {
    display: grid;
    gap: 18px;
}

.instance-heading {
    display: flex;
    gap: 20px;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 18px;
}

.instance-heading span {
    color: #52717b;
    font-family: 'DM Mono', monospace;
    font-size: 12px;
    letter-spacing: 0.12em;
}

.instance-heading h3 {
    margin: 3px 0 0;
    color: #dce8eb;
    font-size: 14px;
}

.notification-test-row {
    display: flex;
    align-items: center;
    gap: 16px;
    width: 100%;
}

.notification-test-row :deep(.n-button) {
    flex-shrink: 0;
}

.settings-card .notification-test-row :deep(.n-alert) {
    flex: 1;
    min-width: 0;
    margin-bottom: 0;
}

.notification-test-row :deep(.n-alert-body),
.settings-footer-alerts :deep(.n-alert-body) {
    padding-top: 6px;
    padding-bottom: 6px;
    line-height: 22px;
}

.notification-test-row :deep(.n-alert__icon),
.settings-footer-alerts :deep(.n-alert__icon) {
    top: 50%;
    margin-top: 0;
    transform: translateY(-50%);
}

.settings-footer {
    display: flex;
    align-items: center;
    gap: 16px;
}

.settings-footer-alerts {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.settings-card .settings-footer-alerts :deep(.n-alert) {
    margin-bottom: 0;
}

.settings-actions {
    display: flex;
    flex-shrink: 0;
    margin-left: auto;
    gap: 10px;
    justify-content: flex-end;
}

@media (max-width: 640px) {
    .settings-footer {
        flex-direction: column;
        align-items: stretch;
    }

    .instance-heading span {
        font-size: 12px;
    }

    .settings-grid {
        grid-template-columns: 1fr;
    }

    .span-2 {
        grid-column: auto;
    }

    .instance-heading {
        align-items: flex-start;
        flex-direction: column;
    }
}
</style>
