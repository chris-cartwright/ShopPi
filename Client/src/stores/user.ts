import { writable } from "svelte/store";
import { UserPreferences, Users } from "../util";

export const user = writable<Users>(Users.Chris);
export const preferences = writable<UserPreferences | null>(null);
