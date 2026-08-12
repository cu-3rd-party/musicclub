import {redirect} from "@sveltejs/kit";
import {DEFAULT_APP_PAGE} from "$lib/config";

export function load() {
    redirect(303, DEFAULT_APP_PAGE);
}
