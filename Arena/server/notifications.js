'use strict';

const { EventEmitter } = require('node:events');

const DEFAULT_WEBHOOK_TEMPLATE = JSON.stringify({
    title: '{{title}}', body: '{{body}}',
}, null, 2);
const TEMPLATE_FIELDS = ['title', 'body', 'runId', 'status', 'finishedAt', 'srvproId', 'note'];

function renderWebhookUrl(template, payload) {
    const rendered = template.replace(/\{\{([^{}]+)\}\}/g, (match, field) => {
        if (!TEMPLATE_FIELDS.includes(field)) throw new Error(`Unknown notification template variable: ${field}`);
        return encodeURIComponent(String(payload[field] ?? ''));
    });
    const url = new URL(rendered);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.hash) {
        throw new Error('Webhook must be an HTTP(S) URL without embedded credentials or fragments');
    }
    return rendered;
}

function renderWebhookTemplate(template, payload) {
    // Substitute parsed string values so quotes and newlines in run text cannot change the JSON structure.
    return JSON.parse(template, (key, value) => typeof value === 'string'
        ? value.replace(/\{\{([^{}]+)\}\}/g, (match, field) => {
            if (!TEMPLATE_FIELDS.includes(field)) throw new Error(`Unknown notification template variable: ${field}`);
            return String(payload[field] ?? '');
        })
        : value);
}

class Notifications extends EventEmitter {
    constructor(database, fetchImpl = fetch) {
        super();
        this.setMaxListeners(0);
        this.database = database;
        this.fetch = fetchImpl;
    }

    async send(run) {
        const settings = this.database.getArenaSettings().settings.notifications;
        if (!settings || settings.mode === 'off') return;
        const statusLabel = { completed: 'completed', failed: 'failed', stopped: 'stopped', interrupted: 'interrupted' }[run.status];
        if (!statusLabel) return;
        const payload = {
            title: `Skynet Arena · Run ${statusLabel}`,
            body: `${run.note || run.id} (${run.srvproId})`,
            runId: run.id, status: run.status, finishedAt: run.finishedAt,
            srvproId: run.srvproId, note: run.note || '',
        };
        await this.deliver(payload, settings);
    }

    async deliver(payload, settings) {
        if (settings.mode === 'frontend') {
            this.emit('notification', payload);
            return;
        }
        if (settings.mode !== 'webhook') return;
        const method = settings.webhookMethod || 'POST';
        const headers = JSON.parse(settings.webhookHeaders?.trim() || '{}');
        const response = await this.fetch(renderWebhookUrl(settings.webhookUrl, payload), {
            method,
            headers: method === 'POST' ? { ...headers, 'Content-Type': 'application/json' } : headers,
            ...(method === 'POST' ? { body: JSON.stringify(renderWebhookTemplate(settings.webhookTemplate, payload)) } : {}),
            signal: AbortSignal.timeout(10000), redirect: 'error',
        });
        await response.body?.cancel();
        if (!response.ok) throw new Error(`Webhook HTTP ${response.status}`);
    }
}

module.exports = { Notifications, DEFAULT_WEBHOOK_TEMPLATE, renderWebhookTemplate, renderWebhookUrl };
