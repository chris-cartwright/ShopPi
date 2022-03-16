<script lang="ts">
    import { Spotify } from "./api";
    import { derived, readable, Readable, writable } from "svelte/store";
    import ky from "ky";
    import Pulse from "@svicons/open-iconic/pulse.svelte";
    import MediaPlay from "@svicons/open-iconic/media-play.svelte";
    import MediaPause from "@svicons/open-iconic/media-pause.svelte";
    import MediaSkipBackward from "@svicons/open-iconic/media-skip-backward.svelte";
    import MediaSkipForward from "@svicons/open-iconic/media-skip-forward.svelte";
    import Heart from "@svicons/open-iconic/heart.svelte";

    class State {
        static readonly Empty = new State(false, "", "", "", null);

        public constructor(
            public readonly playing: boolean,
            public readonly songId: string | "",
            public readonly songTitle: string | "",
            public readonly songArtist: string | "",
            public readonly isFavourite: boolean | null
        ) {}
    }

    let enabled = false;
    let authenticated = Spotify.isAuthenticated;
    let api: typeof ky | null = null;
    let token: string;
    let state = writable<State>(State.Empty);
    let playing = derived(state, (s) => s.playing);
    let statusTimer: NodeJS.Timer | null = null;
    let disableCountdown: Readable<number> | null = null;
    let formattedDisabledCountdown: Readable<string> | null = null;

    (async function () {
        token = await Spotify.getToken();
        api = ky.extend({
            prefixUrl: "https://api.spotify.com/v1/me",
            headers: {
                Authorization: `Bearer ${token}`,
            },
        });

        // Keep the token refreshed while active.
        setInterval(async () => {
            token = await Spotify.getToken();
        }, 60_000);
    })();

    function startStatusLoop() {
        statusTimer = setInterval(updateStatus, 1_000);
    }

    function stopStatusLoop() {
        clearInterval(statusTimer);
    }

    function enable() {
        enabled = true;
        startStatusLoop();

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
        let url = await Spotify.getLoginUrl();
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
                currentState.songId,
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
                json.item.id,
                json.item.name,
                json.item.artists.map((a) => a.name).join(", "),
                currentState.isFavourite
            )
        );

        if (currentState.songId != json.item.id) {
            updateFavourite();
        }
    }
</script>

<div class="container">
    {#if $authenticated}
        <div class="row align-items-center">
            <div class="col-1">
                <button
                    class="btn {enabled ? 'btn-success' : 'btn-secondary'}"
                    class:active={enabled}
                    on:click={toggleEnabled}
                >
                    <Pulse width="1em" />
                </button>
            </div>
            <div class="col-1">
                <button
                    class="btn btn-primary"
                    on:click={playPause}
                    disabled={!enabled}
                >
                    {#if $playing}
                        <MediaPause width="1em" />
                    {:else}
                        <MediaPlay width="1em" />
                    {/if}
                </button>
            </div>
            <div class="col-1">
                <button
                    class="btn btn-secondary"
                    on:click={previous}
                    disabled={!enabled}
                >
                    <MediaSkipBackward width="1em" />
                </button>
            </div>
            <div class="col-1">
                <button
                    class="btn btn-secondary"
                    on:click={next}
                    disabled={!enabled}
                >
                    <MediaSkipForward width="1em" />
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
                    <Heart width="1em" />
                </button>
            </div>
            <div class="col-6">
                <div class="container">
                    <div class="row">
                        {#if $state.playing}
                            {$state.songTitle} - {$state.songArtist}
                        {/if}
                    </div>
                </div>
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
        </div>
    {:else}
        <div class="row">
            <p>
                No account information found. Please click <a
                    href="/"
                    on:click|preventDefault={login}>here</a
                > to log in.
            </p>
        </div>
    {/if}
</div>
