<script lang="ts">
    import Icon from "@iconify/svelte";
    import dayjs, { Dayjs } from "dayjs";
    import duration from "dayjs/plugin/duration";
    import { toast } from "@zerodevx/svelte-toast";
    import { range } from "lodash";

    dayjs.extend(duration);

    class Timer {
        private static _counter = 0;
        private readonly _expires: Dayjs;
        private readonly _id: number;
        private _timer: NodeJS.Timer | null = null;
        private _elapsed = false;

        get id() {
            return this._id;
        }

        get expires() {
            return this._expires;
        }

        get hours() {
            let d = dayjs.duration(this._expires.diff(dayjs()));
            return Math.floor(d.asHours());
        }

        get minutes() {
            let d = dayjs.duration(this._expires.diff(dayjs()));
            return Math.max(d.minutes(), 0);
        }

        get seconds() {
            let d = dayjs.duration(this._expires.diff(dayjs()));
            return Math.max(d.seconds(), 0);
        }

        get elapsed() {
            return this._elapsed;
        }

        constructor(
            hours: number,
            minutes: number,
            seconds: number | null = null,
            public readonly colour: string | null = null
        ) {
            this._id = Timer._counter++;
            this._expires = dayjs().add(hours, "hour").add(minutes, "minute");
            if (seconds != null && seconds > 0) {
                this._expires = this._expires.add(seconds, "seconds");
            }

            this._timer = setInterval(() => {
                this.tick();
            }, 1_000);
        }

        public toDisplayString() {
            let hours = this.hours > 0 ? `${this.hours}:` : "";
            let minutes = this.minutes.toString().padStart(2, "0");
            let seconds = this.seconds.toString().padStart(2, "0");
            return `${hours}${minutes}:${seconds}`;
        }

        public tick() {
            rerenderTimes += 1;
            this._elapsed = this._expires.diff(dayjs(), "seconds") <= 0;
            if (this.elapsed) {
                clearInterval(this._timer);
            }
        }
    }

    // Matches the class names in Bootstrap 5
    const availableColours: string[] = [
        "primary",
        "secondary",
        "success",
        "danger",
        "warning",
        "info",
        //"light", Too light
        "dark",
    ];

    let selectedHours: number = 0;
    let selectedMinutes: number = 0;
    let selectedColour: string | null = null;
    let timers: Timer[] = [];
    let rerenderTimes = 0;

    function addNew() {
        if (selectedMinutes === 0 && selectedHours === 0) {
            toast.push("No time selected.");
            return;
        }

        timers = [
            ...timers,
            new Timer(selectedHours, selectedMinutes, null, selectedColour),
        ];
        selectedHours = 0;
        selectedMinutes = 0;
        selectedColour = null;
    }

    // For debugging.
    window['add5Seconds'] = () => {
        timers = [...timers, new Timer(0, 0, 5)];
    }

    function removeTimer(id: number) {
        timers = timers.filter((t) => t.id !== id);
    }
</script>

<div class="container">
    <div class="row gx-1 my-1">
        <div class="col">
            <div class="input-group mx-auto">
                <button
                    class="btn btn-outline-secondary dropdown-toggle"
                    id="hoursdd"
                    data-bs-toggle="dropdown"
                    aria-expanded="false"
                >
                    {selectedHours}
                </button>
                <div class="dropdown-menu" aria-labelledby="hoursdd">
                    <div class="container">
                        <div class="row row-cols-3 mx-0">
                            {#each [0, 1, 2, 3, 4, 5] as n}
                                <button
                                    class="btn"
                                    on:click={() => (selectedHours = n)}
                                >
                                    {n}
                                </button>
                            {/each}
                        </div>
                    </div>
                </div>
                <span class="input-group-text">hours</span>
            </div>
        </div>
        <div class="col">
            <div class="input-group mx-auto">
                <button
                    class="btn btn-outline-secondary dropdown-toggle"
                    id="minsdd"
                    data-bs-toggle="dropdown"
                    aria-expanded="false"
                >
                    {selectedMinutes}
                </button>
                <div class="dropdown-menu" aria-labelledby="minsdd">
                    <div class="container">
                        <div class="row row-cols-3 mx-0">
                            {#each range(0, 60, 5) as n}
                                <button
                                    class="btn"
                                    on:click={() => (selectedMinutes = n)}
                                >
                                    {n}
                                </button>
                            {/each}
                        </div>
                    </div>
                </div>
                <span class="input-group-text">mins</span>
            </div>
        </div>
    </div>
    <div class="row my-1">
        <div class="col">
            <button
                class="btn btn-outline-secondary dropdown-toggle"
                id="coloursdd"
                data-bs-toggle="dropdown"
                aria-expanded="false"
            >
                <span
                    class="rounded bg-{selectedColour}"
                    style="display: inline-block; width: 1.5em"
                >
                    &nbsp;
                </span>
            </button>
            <!-- This is ugly as fuck, but I'm tired of fighting Bootstrap. #11. -->
            <div class="dropdown-menu" aria-labelledby="coloursdd">
                <div class="container">
                    <div class="row row-cols-4">
                        {#each availableColours as name}
                            <button
                                class="btn btn-{name} col m-1"
                                on:click={() => (selectedColour = name)}
                                >&nbsp;</button
                            >
                        {/each}
                    </div>
                </div>
            </div>
        </div>
        <div class="col">
            <button class="btn btn-primary" on:click={addNew}>
                <Icon icon="material-symbols:add-circle-outline-rounded" /> Add Timer
            </button>
        </div>
    </div>
    {#each timers as item (item.id)}
        <div class="row my-1">
            <div class="col">
                {#key rerenderTimes}
                    <div
                        class="text-center align-middle rounded fs-4 gx-2 {item.elapsed
                            ? 'bg-warning'
                            : ''}"
                    >
                        {item.toDisplayString()}
                    </div>
                {/key}
            </div>
            <div class="col-2">
                <span
                    class="btn bg-{item.colour}"
                    style="cursor: default"
                >
                    <div style="width: 1em; height: 1.5em"></div>
                </span>
            </div>
            <div class="col-2">
                <button
                    class="btn bg-warning"
                    on:click={() => removeTimer(item.id)}
                >
                    <Icon icon="mdi:trash" />
                </button>
            </div>
        </div>
    {/each}
</div>
