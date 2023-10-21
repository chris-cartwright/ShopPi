import { writable } from "svelte/store";
import { Users } from "../util";

export const user = writable<Users>(Users.Chris);