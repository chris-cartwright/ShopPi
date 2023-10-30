<script lang="ts">
    import { user } from "./stores/user";
    import { Apis } from "./api";
    import type { Integrations } from "./util";
    import ReloadWindowButton from "./ReloadWindowButton.svelte";

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
            <p><ReloadWindowButton /></p>
        </div>
    </div>
{/if}
