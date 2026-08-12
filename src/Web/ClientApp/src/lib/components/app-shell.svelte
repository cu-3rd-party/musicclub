<script lang="ts">
    import type {Snippet} from "svelte";
    import {authState} from "$lib/auth/store";
    import AppSidebar from "$lib/components/app-sidebar.svelte";
    import * as Breadcrumb from "$lib/components/ui/breadcrumb";
    import {Separator} from "$lib/components/ui/separator";
    import * as Sidebar from "$lib/components/ui/sidebar";
    import {cn} from "$lib/utils";

    type BreadcrumbItem = {
        label: string;
        href?: string;
    };

    let {
        class: className,
        breadcrumbs = [],
        toolbar,
        children,
    }: {
        class?: string;
        breadcrumbs?: BreadcrumbItem[];
        toolbar?: Snippet;
        children?: Snippet;
    } = $props();
</script>

<Sidebar.Provider>
    <AppSidebar currentUser={$authState.user}/>
    <Sidebar.Inset class="bg-background min-h-screen">
        <header
            class="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-12"
        >
            <div class="flex items-center gap-2 px-4">
                <Sidebar.Trigger class="-ms-1"/>
                {#if breadcrumbs.length > 0}
                    <Separator orientation="vertical" class="me-2 data-[orientation=vertical]:h-4"/>
                    <Breadcrumb.Root>
                        <Breadcrumb.List>
                            {#each breadcrumbs as item, index (item.label)}
                                <Breadcrumb.Item class={index < breadcrumbs.length - 1 ? "hidden md:block" : ""}>
                                    {#if item.href && index < breadcrumbs.length - 1}
                                        <Breadcrumb.Link href={item.href}>{item.label}</Breadcrumb.Link>
                                    {:else}
                                        <Breadcrumb.Page>{item.label}</Breadcrumb.Page>
                                    {/if}
                                </Breadcrumb.Item>
                                {#if index < breadcrumbs.length - 1}
                                    <Breadcrumb.Separator class="hidden md:block"/>
                                {/if}
                            {/each}
                        </Breadcrumb.List>
                    </Breadcrumb.Root>
                {/if}
            </div>
            <div class="flex items-center gap-2 px-4">
                {@render toolbar?.()}
            </div>
        </header>
        <main class={cn("flex flex-1 flex-col px-4 pb-10 pt-4 md:px-8", className)}>
            {@render children?.()}
        </main>
    </Sidebar.Inset>
</Sidebar.Provider>
