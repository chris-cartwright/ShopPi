<script lang="ts">
    import { Spotify, Users } from "./api";
    import {
        derived,
        readable,
        Readable,
        Unsubscriber,
        writable,
    } from "svelte/store";
    import ky from "ky";
    import Icon from "@iconify/svelte";

    class State {
        static readonly Empty = new State(false, null, "", null, "", "", null);

        public constructor(
            public readonly playing: boolean,
            public readonly progressMs: number | null,
            public readonly songId: string | "",
            public readonly durationMs: number | null,
            public readonly songTitle: string | "",
            public readonly songArtist: string | "",
            public readonly isFavourite: boolean | null
        ) {}
    }

    let enabled = false; // Pinging Spotify for current track info
    let user = writable<Users>("Chris");
    let authenticated = writable<boolean>(false);
    let authenticatedUnsub: Unsubscriber | null = null;
    let api: typeof ky | null = null;
    let token: string;
    let tokenInterval: NodeJS.Timer | null = null;
    let state = writable<State>(State.Empty);
    let playing = derived(state, (s) => s.playing);
    let statusTimer: NodeJS.Timer | null = null;
    let disableCountdown: Readable<number> | null = null;
    let formattedDisabledCountdown: Readable<string> | null = null;
    $: formattedProgress = formatMilliseconds($state.progressMs);
    $: formattedDuration = formatMilliseconds($state.durationMs);

    user.subscribe(async (u) => {
        if (enabled) {
            // Reset the GUI and force the user to click the Connect button.
            disable();
        }

        if (authenticatedUnsub != null) {
            authenticatedUnsub();
        }

        authenticatedUnsub = Spotify.isAuthenticated(u).subscribe((auth) => {
            authenticated.set(auth);
        });

        getToken();
    });

    (async function () {
        $user = "Chris";
    })();

    async function getToken() {
        token = await Spotify.getToken($user);
        api =
            token == null
                ? null
                : ky.extend({
                      prefixUrl: "https://api.spotify.com/v1/me",
                      headers: {
                          Authorization: `Bearer ${token}`,
                      },
                  });
    }

    function startTokenLoop() {
        getToken();
        // Keep the token refreshed while active.
        tokenInterval = setInterval(getToken, 60_000);
    }

    function stopTokenLoop() {
        clearInterval(tokenInterval);
    }

    function startStatusLoop() {
        statusTimer = setInterval(updateStatus, 1_000);
    }

    function stopStatusLoop() {
        clearInterval(statusTimer);
    }

    function enable() {
        enabled = true;
        startStatusLoop();
        startTokenLoop();

        // In seconds, 3 hours
        let duration = 10_800;
        disableCountdown = readable(duration, function start(set) {
            const interval = setInterval(() => {
                duration--;
                if (duration <= 0) {
                    disable();
                }

                set(duration);
            }, 1_000);

            return function stop() {
                clearInterval(interval);
            };
        });

        formattedDisabledCountdown = derived(disableCountdown, (value) => {
            var hours = Math.floor(value / 3600);
            var minutes = Math.floor((value - hours * 3600) / 60);
            var seconds = value - hours * 3600 - minutes * 60;

            if (hours > 0) {
                // Rounding up to show rough numbers
                return hours + 1 + "h";
            }

            if (minutes > 9) {
                return minutes + "m";
            }

            return `${minutes}m ${seconds}s`;
        });
    }

    function disable() {
        enabled = false;
        stopStatusLoop();
        stopTokenLoop();
        disableCountdown = null;
        formattedDisabledCountdown = null;
        state.set(State.Empty);
    }

    function toggleEnabled() {
        (enabled ? disable : enable)();
    }

    async function playPause() {
        if ($state.playing) {
            await api.put("player/pause");
        } else {
            await api.put("player/play");
        }

        await updateStatus();
    }

    async function previous() {
        await api.post("player/previous");
    }

    async function next() {
        await api.post("player/next");
    }

    async function toggleFavourite() {
        let method: keyof typeof api = $state.isFavourite ? "delete" : "put";
        await api[method]("tracks", { searchParams: { ids: $state.songId } });
        updateFavourite();
    }

    async function login() {
        let url = await Spotify.getLoginUrl($user);
        window.location.href = url;
    }

    async function updateFavourite() {
        let songId = $state.songId;
        let response = await api.get("tracks/contains", {
            searchParams: { ids: songId },
        });

        let arr = await response.json();
        let favourite = arr.length > 0 && arr[0] === true;

        const currentState = $state;
        state.set(
            new State(
                currentState.playing,
                currentState.progressMs,
                currentState.songId,
                currentState.durationMs,
                currentState.songTitle,
                currentState.songArtist,
                favourite
            )
        );
    }

    async function updateStatus() {
        let response = await api.get("player/currently-playing");
        if (response.status == 204) {
            state.set(State.Empty);
            return;
        }

        let json = await response.json();

        const currentState = $state;

        state.set(
            new State(
                json.is_playing,
                json.progress_ms,
                json.item.id,
                json.item.duration_ms,
                json.item.name,
                json.item.artists.map((a) => a.name).join(", "),
                currentState.isFavourite
            )
        );

        if (currentState.songId != json.item.id) {
            updateFavourite();
        }
    }

    function formatMilliseconds(milliseconds: number): string {
        let seconds = milliseconds / 1000;
        let minutes = Math.floor(seconds / 60);
        seconds -= minutes * 60;
        seconds = Math.floor(seconds);

        return (
            String(minutes).padStart(2, "0") +
            ":" +
            String(seconds).padStart(2, "0")
        );
    }

    function toPercent(current: number, total: number): string {
        let percent = current / total;
        if (Number.isNaN(percent)) {
            percent = 0;
        }

        return Math.floor(percent * 100) + "%";
    }
</script>

<div class="container">
    <div class="row">
        <div class="btn-group">
            <input
                type="radio"
                class="btn-check"
                name="user"
                bind:group={$user}
                value="Chris"
                id="chris"
                autocomplete="off"
                checked
            />
            <label class="btn btn-outline-primary" for="chris"> Chris </label>

            <input
                type="radio"
                class="btn-check"
                name="user"
                bind:group={$user}
                value="Courtney"
                id="courtney"
                autocomplete="off"
            />
            <label class="btn btn-outline-primary" for="courtney">
                Courtney
            </label>
        </div>
    </div>
    <div class="row m-3">
        {#if $authenticated}
            <div class="container">
                <div class="row align-items-center">
                    <div class="col-1">
                        <button
                            class="btn {enabled
                                ? 'btn-success'
                                : 'btn-secondary'}"
                            class:active={enabled}
                            on:click={toggleEnabled}
                        >
                            <Icon icon="majesticons:pulse" />
                        </button>
                    </div>
                    <div class="col-1">
                        <button
                            class="btn btn-primary"
                            on:click={playPause}
                            disabled={!enabled}
                        >
                            {#if $playing}
                                <Icon icon="material-symbols:pause-circle" />
                            {:else}
                                <Icon icon="material-symbols:play-circle" />
                            {/if}
                        </button>
                    </div>
                    <div class="col-1">
                        <button
                            class="btn btn-secondary"
                            on:click={previous}
                            disabled={!enabled}
                        >
                            <Icon icon="material-symbols:skip-previous" />
                        </button>
                    </div>
                    <div class="col-1">
                        <button
                            class="btn btn-secondary"
                            on:click={next}
                            disabled={!enabled}
                        >
                            <Icon icon="material-symbols:skip-next" />
                        </button>
                    </div>
                    <div class="col-1">
                        <button
                            class="btn {$state.isFavourite
                                ? 'btn-success'
                                : 'btn-secondary'}"
                            on:click={toggleFavourite}
                            disabled={!enabled}
                        >
                            <Icon icon="material-symbols:favorite" />
                        </button>
                    </div>
                    <div class="col-1">
                        <div class="text-secondary">
                            {#if enabled}
                                {$formattedDisabledCountdown}
                            {:else}
                                N/A
                            {/if}
                        </div>
                    </div>
                    <div class="col-4">
                        <div class="text-secondary">
                            {formattedProgress} / {formattedDuration} - {toPercent(
                                $state.progressMs,
                                $state.durationMs
                            )}
                        </div>
                    </div>
                </div>
            </div>
        {/if}
    </div>
    <div class="row m-3">
        {#if $authenticated}
            <div class="container">
                <div class="row">
                    {#if $state.playing}
                        {$state.songTitle} - {$state.songArtist}
                    {:else}
                        <!-- Keep the vertical space -->
                        &nbsp;
                    {/if}
                </div>
            </div>
        {:else}
            <p>
                No account information found. Please click
                <a href="/" on:click|preventDefault={login}>here</a>
                to log in.
            </p>
            <p>
                <button
                    class="btn btn-primary"
                    on:click={() => window.location.reload()}
                >
                    <Icon icon="ion:reload" /> Reload
                </button>
            </p>
        {/if}
    </div>
</div>
