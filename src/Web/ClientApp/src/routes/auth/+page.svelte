<script lang="ts">
    import type {PageData} from "./$types";
    import LoginForm from "$lib/components/LoginForm.svelte";

    let {data}: {data: PageData} = $props();
</script>

<LoginForm deeplink={data.deeplink}/>