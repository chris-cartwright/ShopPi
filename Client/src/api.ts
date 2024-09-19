import { Readable, writable, derived, get, readonly } from "svelte/store";
import { user } from "./stores/user";
import { Integrations, UserPreferences, Users } from "./util";
import _ from "lodash";

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

type ApiOptions = RequestInit & { method: 'GET' | 'PUT' | 'DELETE', 'api-key'?: string };

async function apiFetch(url: string, opts: ApiOptions = { method: 'GET' }): Promise<ReturnType<typeof fetch>> {
    let fullUrl = prefixServerUrl(url);
    let local = _.cloneDeep(opts);
    if (local.headers == null) {
        local.headers = {};
    }

    local.headers['X-Api-Key'] = local["api-key"] ?? getApiKey();
    return await fetch(fullUrl, local);
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

let checkTokenInterval: NodeJS.Timer | null = null;
export function configureTokens() {
    if(checkTokenInterval != null) {
        return;
    }
    
    // `configureTokens` should not be blocking, but we also want to wait for
    // one token check before starting the loop.
    (async () => {
        await checkTokens();
        checkTokenInterval = setInterval(() => checkTokens(), 5_000);
    })();
}

async function getToken(user: Users, integration: Integrations): Promise<string | null> {
    let response = await apiFetch(`/${integration}/token?user=${user}`);
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
        let response = await apiFetch(`/echo?msg=${encodeURI(msg)}`, { method: 'GET', 'api-key': apiKey });
        if (response.status != 200) {
            console.error('Echo returned an error response', response);
            return false;
        }

        return await response.json() == `Echo: ${msg}`;
    },
    async loadPreferences(user: Users): Promise<UserPreferences> {
        let response = await apiFetch(`/preferences?user=${user}`);
        if (response.status !== 200) {
            console.error('Failed to load preferences.');
            return;
        }

        return (await response.json()) as UserPreferences;
    },
    async savePreferences(user: Users, preferences: UserPreferences): Promise<boolean> {
        let response = await apiFetch(
            `/preferences?user=${user}`,
            {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json; charset=utf-8'
                },
                body: JSON.stringify(preferences)
            }
        );
        return response.status === 200;
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
            let response = await apiFetch(`/${integration}/authorize?user=${user}`);
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
