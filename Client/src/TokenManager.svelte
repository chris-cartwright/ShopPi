<script lang="ts">
    import { user } from "./stores/user";
    import { Apis } from "./api";
    import Icon from "@iconify/svelte";
    import type { Integrations } from "./util";

    export let integration: Integrations;

    const hasToken = Apis[integration].hasToken($user);

    async function login() {
        let url = await Apis[integration].getLoginUrl($user);
        window.location.href = url;
    }
</script>

{#if $hasToken}
    <slot />
{:else}
    <div class="container">
        <div class="row">
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
        </div>
    </div>
{/if}
