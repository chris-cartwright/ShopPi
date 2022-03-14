<script lang="ts">
	import { General } from "./api";
	import Spotify from "./Spotify.svelte";
	import { random } from "./util";
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
</script>

<main>
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
		<Spotify />
	{/if}
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
	}
</style>
