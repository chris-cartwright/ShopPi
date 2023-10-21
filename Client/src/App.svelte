<script lang="ts">
    import { onMount } from "svelte";
	import { General, configureTokens } from "./api";
	import Spotify from "./Spotify.svelte";
	import { user } from "./stores/user";
	import Timers from "./Timers.svelte";
	import ToDo from "./ToDo.svelte";
	import TokenManager from "./TokenManager.svelte";
	import { Integrations, random } from "./util";
	import {
		SvelteToast,
		SvelteToastOptions,
		toast,
	} from "@zerodevx/svelte-toast";

	let hasApiKey = localStorage.getItem("api-key") != null;
	let apiKey: string;
	let toastOpts: SvelteToastOptions = {
		pausable: true,
		duration: 6_000,
	};

	async function testAndSave() {
		let success = await General.echo(random(10), apiKey);
		if (success) {
			localStorage.setItem("api-key", apiKey);
			hasApiKey = true;
		} else {
			toast.push("Invalid API key.", {
				theme: {
					"--toastBackground": "#F56565",
					"--toastBarBackground": "#C53030",
				},
			});
		}
	}

	onMount(async() => {
		configureTokens();
	});
</script>

<main>
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
	{#if !hasApiKey}
		<div class="alert alert-warning" role="alert">
			<p>Missing API key!</p>
			<p>
				<code>
					localStorage.setItem('api-key', '&lt;api key here&gt;');
				</code>
			</p>
			<div>
				<input placeholder="API key" type="text" bind:value={apiKey} />
				<button on:click={testAndSave}>Test and Save</button>
			</div>
		</div>
	{:else}
		<TokenManager integration={Integrations.Spotify}>
			<Spotify />
		</TokenManager>
	{/if}

	<div class="row">
		<div class="col-5"><Timers /></div>
		<div class="col">
			{#if !hasApiKey}
				<!-- API key management is handled in the Spotify section above -->
				&nbsp;
			{:else}
				<TokenManager integration={Integrations.ToDo}>
					<ToDo />
				</TokenManager>
			{/if}
		</div>
	</div>
	<SvelteToast options={toastOpts} />
</main>

<style>
	main {
		text-align: center;
		padding: 0.5em;
		width: 800px;
		height: 480px;
		margin: 0 auto;
		background-color: white;
	}

	:global(body) {
		background-color: grey !important;
		padding: 0 !important;
		margin: 0 !important;
	}
</style>
