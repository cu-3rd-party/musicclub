<script lang="ts">
    import {goto} from "$app/navigation";
    import {page} from "$app/state";
    import {cn} from "$lib/utils";
    import type {Component} from "svelte";

    type NavItem = {
        label: string;
        href: string;
        icon: Component;
    };

    let {
        items,
        class: className,
    }: {
        items: NavItem[];
        class?: string;
    } = $props();

    const currentPath = $derived(page.url.pathname);

    function isActive(href: string): boolean {
        return currentPath === href || currentPath.startsWith(href + "/");
    }

    async function navigate(href: string, event: MouseEvent): Promise<void> {
        event.preventDefault();
        await goto(href);
    }
</script>

<nav class={cn("bg-background border-t border-border", className)} aria-label="Main navigation">
    <ul class="flex items-stretch justify-around">
        {#each items as item (item.href)}
            {@const active = isActive(item.href)}
            <li class="flex-1">
                <a
                    href={item.href}
                    class={cn(
                        "flex flex-col items-center gap-1 px-2 py-2 text-xs font-medium transition-colors",
                        "outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background",
                        active
                            ? "text-primary"
                            : "text-muted-foreground hover:text-foreground",
                    )}
                    aria-current={active ? "page" : undefined}
                    onclick={(e) => navigate(item.href, e)}
                >
                    <item.icon class="size-5" strokeWidth={active ? 2.5 : 2} />
                    <span>{item.label}</span>
                </a>
            </li>
        {/each}
    </ul>
</nav>
