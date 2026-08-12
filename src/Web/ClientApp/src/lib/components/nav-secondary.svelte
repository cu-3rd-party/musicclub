<script lang="ts">
    import {resolve} from "$app/paths";
    import type {NavUrl} from "./app-sidebar.svelte";
    import * as Sidebar from "$lib/components/ui/sidebar";
    import type {Component, ComponentProps} from "svelte";

    const isExternalUrl = (url: NavUrl): url is `http${string}` => url.startsWith("http");

    let {
        ref = $bindable(null),
        items,
        ...restProps
    }: {
        items: {
            title: string;
            url: NavUrl;
            icon: Component;
        }[];
    } & ComponentProps<typeof Sidebar.Group> = $props();
</script>

<Sidebar.Group bind:ref {...restProps}>
    <Sidebar.GroupContent>
        <Sidebar.Menu>
            {#each items as item (item.title)}
                {@const Icon = item.icon}
                <Sidebar.MenuItem>
                    <Sidebar.MenuButton size="sm">
                        {#snippet child({props})}
                            {#if item.url === "#"}
                                <button type="button" {...props}>
                                    <Icon/>
                                    <span>{item.title}</span>
                                </button>
                            {:else if isExternalUrl(item.url)}
                                <a href={item.url} rel="external" {...props}>
                                    <Icon/>
                                    <span>{item.title}</span>
                                </a>
                            {:else}
                                <a href={resolve(item.url)} {...props}>
                                    <Icon/>
                                    <span>{item.title}</span>
                                </a>
                            {/if}
                        {/snippet}
                    </Sidebar.MenuButton>
                </Sidebar.MenuItem>
            {/each}
        </Sidebar.Menu>
    </Sidebar.GroupContent>
</Sidebar.Group>
