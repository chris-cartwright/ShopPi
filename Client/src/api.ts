import { writable } from "svelte/store";

const isAuthenticatedStore = writable(false);

function getApiKey() {
    const key = localStorage.getItem('api-key');
    if (key == null) {
        throw new Error('Missing API key.');
    }

    return key;
}

function prefixServerUrl(path: string) {
    // TODO: Default URL should be in a config file.
    let server = localStorage.getItem('server-url') || 'http://localhost:4000/api/';
    if (path.indexOf('/') === 0) {
        path = path.substring(1);
    }

    return server + path;
}

type ApiOptions = Omit<Parameters<typeof fetch>[1], 'method'> & { 'api-key'?: string };

async function apiGet(url: string, opts: ApiOptions = {}): ReturnType<typeof fetch> {
    let fullUrl = prefixServerUrl(url);
    let composed = { ...opts, ...{ method: 'GET' } };
    if (composed.headers == null) {
        composed.headers = {};
    }

    composed.headers['X-Api-Key'] = opts["api-key"] ?? getApiKey();
    return await fetch(fullUrl, composed);
}

async function apiPost(url: string, opts: ApiOptions = {}): ReturnType<typeof fetch> {
    let fullUrl = prefixServerUrl(url);
    let composed = { ...opts, ...{ method: 'POST' } };
    if (composed.headers == null) {
        composed.headers = {};
    }

    composed.headers['X-Api-Key'] = opts["api-key"] ?? getApiKey();
    return await fetch(fullUrl, composed);
}

export const General = {
    async echo(msg: string, apiKey?: string): Promise<boolean> {
        let response = await apiGet('/echo?msg=' + encodeURI(msg), { 'api-key': apiKey });
        if (response.status != 200) {
            console.error('Echo returned an error response', response);
            return false;
        }

        return await response.json() == `Echo: ${msg}`;
    }
}

export const Spotify = {
    isAuthenticated: { subscribe: isAuthenticatedStore.subscribe },
    async getToken(): Promise<string | null> {
        let response = await apiGet('/spotify/token');
        if (response.status == 404) {
            isAuthenticatedStore.set(false);
            return null;
        }

        if (response.status != 200) {
            isAuthenticatedStore.set(false);
            console.error('Could not get token.', response);
            return null;
        }

        isAuthenticatedStore.set(true);
        return response.json();
    },
    async getLoginUrl(): Promise<string> {
        let response = await apiGet('/spotify/authorize');
        if (response.status != 200) {
            console.error('Could not get authorization URL.', response);
            throw new Error('Could not get authorization URL.');
        }

        return response.json();
    }
};