<script lang="ts">
    import {resolve} from "$app/paths";
    import type {NavUrl} from "./app-sidebar.svelte";
    import * as Collapsible from "$lib/components/ui/collapsible/index.js";
    import * as Sidebar from "$lib/components/ui/sidebar/index.js";
    import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";

    const isExternalUrl = (url: NavUrl): url is `http${string}` => url.startsWith("http");

    let {
        items,
    }: {
        items: {
            title: string;
            url: NavUrl;
            onSelect?: () => void;
            // This should be `Component` after @lucide/svelte updates types
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            icon: any;
            isActive?: boolean;
            items?: {
                title: string;
                url: NavUrl;
                onSelect?: () => void;
            }[];
        }[];
    } = $props();
</script>

<Sidebar.Group>
    <Sidebar.Menu class="gap-2">
        {#each items as mainItem (mainItem.title)}
            {@const Icon = mainItem.icon}
            <Collapsible.Root open={mainItem.isActive}>
                {#snippet child({props})}
                    <Sidebar.MenuItem {...props}>
                        <Sidebar.MenuButton tooltipContent={mainItem.title}>
                            {#snippet child({props})}
                                {#if mainItem.url === "#"}
                                    <button type="button" {...props}>
                                        <Icon/>
                                        <span>{mainItem.title}</span>
                                    </button>
                                {:else if isExternalUrl(mainItem.url)}
                                    <a href={mainItem.url} rel="external" {...props}>
                                        <Icon/>
                                        <span>{mainItem.title}</span>
                                    </a>
                                {:else}
                                    <a href={resolve(mainItem.url)} {...props}>
                                        <Icon/>
                                        <span>{mainItem.title}</span>
                                    </a>
                                {/if}
                            {/snippet}
                        </Sidebar.MenuButton>
                        {#if mainItem.items?.length}
                            <Collapsible.Trigger>
                                {#snippet child({props})}
                                    <Sidebar.MenuAction
                                            {...props}
                                            class="data-[state=open]:rotate-90"
                                    >
                                        <ChevronRightIcon/>
                                        <span class="sr-only">Toggle</span>
                                    </Sidebar.MenuAction>
                                {/snippet}
                            </Collapsible.Trigger>
                            <Collapsible.Content>
                                <Sidebar.MenuSub class="gap-1">
                                    {#each mainItem.items as subItem (subItem.title)}
                                        <Sidebar.MenuSubItem>
                                            {#if subItem.onSelect}
                                                <Sidebar.MenuSubButton onclick={subItem.onSelect}>
                                                    <span>{subItem.title}</span>
                                                </Sidebar.MenuSubButton>
                                            {:else if subItem.url === "#"}
                                                <Sidebar.MenuSubButton>
                                                    <span>{subItem.title}</span>
                                                </Sidebar.MenuSubButton>
                                            {:else if isExternalUrl(subItem.url)}
                                                <Sidebar.MenuSubButton href={subItem.url} rel="external">
                                                    <span>{subItem.title}</span>
                                                </Sidebar.MenuSubButton>
                                            {:else}
                                                <Sidebar.MenuSubButton href={resolve(subItem.url)}>
                                                    <span>{subItem.title}</span>
                                                </Sidebar.MenuSubButton>
                                            {/if}
                                        </Sidebar.MenuSubItem>
                                    {/each}
                                </Sidebar.MenuSub>
                            </Collapsible.Content>
                        {/if}
                    </Sidebar.MenuItem>
                {/snippet}
            </Collapsible.Root>
        {/each}
    </Sidebar.Menu>
</Sidebar.Group>
