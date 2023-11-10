<script lang="ts">
    import { onMount } from "svelte";
    import { get } from "svelte/store";
    import { General, ToDo } from "./api";
    import {
        AuthenticationProvider,
        Client,
        GraphRequest,
        PageIterator,
    } from "@microsoft/microsoft-graph-client";
    import { preferences, user } from "./stores/user";
    import type {
        TodoTask,
        TodoTaskList,
    } from "@microsoft/microsoft-graph-types";
    import { toast } from "@zerodevx/svelte-toast";
    import _ from "lodash";
    import Icon from "@iconify/svelte";

    class AccessTokenProvider implements AuthenticationProvider {
        getAccessToken(): Promise<string> {
            return Promise.resolve(get(ToDo.token(get(user))));
        }
    }

    let client: Client | null = null;
    let tasklist: TodoTaskList | null = null;
    let tasklistTasks: TodoTask[] = [];
    let tasklists: TodoTaskList[] = [];

    async function collectItems<T>(request: GraphRequest): Promise<T[]> {
        let ret: T[] = [];
        const pageIterator = new PageIterator(
            client,
            await request.get(),
            (item) => {
                ret.push(item);
                return true;
            }
        );

        await pageIterator.iterate();
        return ret;
    }

    async function load() {
        tasklist = await client
            .api(`/me/todo/lists/${$preferences.todo.taskListId}`)
            .get();
        let unsorted: TodoTask[] = await collectItems(
            client.api(`/me/todo/lists/${$preferences.todo.taskListId}/tasks`)
        );
        tasklistTasks = unsorted.sort((a, b) => {
            if (a.completedDateTime == null && b.completedDateTime != null) {
                return -1;
            }

            if (a.completedDateTime == null && b.completedDateTime == null) {
                return 0;
            }

            return 1;
        });
    }

    async function selectList(list: TodoTaskList) {
        $preferences.todo.taskListId = list.id;
        await General.savePreferences($user, $preferences);
        toast.push("Preferences saved.");
        await load();
    }

    async function clearList() {
        $preferences.todo.taskListId = null;
        await General.savePreferences($user, $preferences);
        toast.push("Preferences saved.");
        tasklist = null;
        tasklistTasks = [];
        tasklists = await collectItems(client.api("/me/todo/lists"));
    }

    async function toggleState(task: TodoTask) {
        await client
            .api(
                `/me/todo/lists/${$preferences.todo.taskListId}/tasks/${task.id}`
            )
            .update({
                status:
                    task.completedDateTime == null ? "completed" : "notStarted",
            });
        await load();
    }

    onMount(async () => {
        client = Client.initWithMiddleware({
            authProvider: new AccessTokenProvider(),
        });

        if (_.isEmpty($preferences.todo.taskListId)) {
            tasklists = await collectItems(client.api("/me/todo/lists"));
        } else {
            await load();
        }
    });
</script>

{#if tasklist != null}
    <div class="container">
        <div class="row">
            <div class="col-10 text-start">
                <h2>{tasklist.displayName}</h2>
            </div>
            <div class="col-2">
                <button class="btn btn-warning" on:click={() => clearList()}>
                    <Icon icon="ic:round-cancel" />
                </button>
            </div>
        </div>
        <div class="row overflow-auto" style="margin-top: 10px; height: 185px">
            <ul class="list-group text-start">
                {#each tasklistTasks as task (task.id)}
                    <li
                        class="list-group-item {task.completedDateTime == null
                            ? ''
                            : 'list-group-item-dark'}"
                    >
                        <input
                            class="form-check-input me-1"
                            type="checkbox"
                            value=""
                            checked={task.completedDateTime != null}
                            on:change={() => toggleState(task)}
                        />
                        {task.title}
                    </li>
                {/each}
            </ul>
        </div>
    </div>
{:else}
    <div class="container">
        <div class="row overflow-auto" style="height: 305px">
            <h2>Select a list</h2>
            <div class="list-group text-start">
                {#each tasklists as list}
                    <button
                        class="list-group-item list-group-item-action"
                        on:click={() => selectList(list)}
                    >
                        {list.displayName}
                    </button>
                {/each}
            </div>
        </div>
    </div>
{/if}
