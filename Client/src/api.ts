import { Readable, writable, derived, get, readonly } from "svelte/store";
import { user } from "./stores/user";
import { Integrations, Users } from "./util";

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

async function apiGet(url: string, opts: ApiOptions = {}): Promise<ReturnType<typeof fetch>> {
    let fullUrl = prefixServerUrl(url);
    let composed = { ...opts, ...{ method: 'GET' } };
    if (composed.headers == null) {
        composed.headers = {};
    }

    composed.headers['X-Api-Key'] = opts["api-key"] ?? getApiKey();
    return await fetch(fullUrl, composed);
}

async function apiPost(url: string, opts: ApiOptions = {}): Promise<ReturnType<typeof fetch>> {
    let fullUrl = prefixServerUrl(url);
    let composed = { ...opts, ...{ method: 'POST' } };
    if (composed.headers == null) {
        composed.headers = {};
    }

    composed.headers['X-Api-Key'] = opts["api-key"] ?? getApiKey();
    return await fetch(fullUrl, composed);
}

const tokenStores = {
    [Users.Chris]: {
        [Integrations.Spotify]: writable<string | null>(null),
        [Integrations.ToDo]: writable<string | null>(null)
    },
    [Users.Courtney]: {
        [Integrations.Spotify]: writable<string | null>(null),
        [Integrations.ToDo]: writable<string | null>(null)
    }
};

async function checkTokens() {
    let u = get(user);
    tokenStores[u][Integrations.Spotify].set(await getToken(u, Integrations.Spotify));
    tokenStores[u][Integrations.ToDo].set(await getToken(u, Integrations.ToDo));
}

export function configureTokens() {
    // `configureTokens` should not be blocking, but we also want to wait for
    // one token check before starting the loop.
    (async () => {
        await checkTokens();
        setInterval(() => checkTokens(), 5_000);
    })();
}

async function getToken(user: Users, integration: Integrations): Promise<string | null> {
    let response = await apiGet(`/${integration}/token?user=${user}`);
    if (response.status == 404) {
        tokenStores[user][integration].set(null);
        return null;
    }

    if (response.status != 200) {
        tokenStores[user][integration].set(null);
        console.error('Could not get token.', response);
        return null;
    }

    let token = await response.json();
    tokenStores[user][integration].set(token);
    return token;
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

export interface IIntegrationApi {
    hasToken(user: Users): Readable<boolean>;
    token(user: Users): Readable<string | null>;
    getLoginUrl(user: Users): Promise<string>;
}

function createIntegrationApi(integration: Integrations): IIntegrationApi {
    return {
        hasToken(user: Users): Readable<boolean> {
            return derived(tokenStores[user][integration], v => v != null);
        },
        token(user: Users): Readable<string | null> {
            return readonly(tokenStores[user][integration]);
        },
        async getLoginUrl(user: Users): Promise<string> {
            let response = await apiGet(`/${integration}/authorize?user=${user}`);
            if (response.status != 200) {
                console.error('Could not get authorization URL.', response);
                throw new Error('Could not get authorization URL.');
            }

            return response.json();
        }
    };
}

export const Spotify = createIntegrationApi(Integrations.Spotify);
export const ToDo = createIntegrationApi(Integrations.ToDo);
export const Apis = {
    [Integrations.Spotify]: Spotify,
    [Integrations.ToDo]: ToDo
};
