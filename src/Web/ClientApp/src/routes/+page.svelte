<script lang="ts">
    import {browser} from "$app/environment";
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import {authState} from "$lib/auth/store";
    import {DEFAULT_APP_PAGE} from "$lib/config";

    $effect(() => {
        if (!browser) {
            return;
        }

        return authState.subscribe((state) => {
            if (!state.ready) {
                return;
            }

            void goto(state.user ? resolve(DEFAULT_APP_PAGE) : resolve("/auth"));
        });
    });
</script>
