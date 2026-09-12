<script lang="ts">
    import {Separator} from "$lib/components/ui/separator";
    import {cn, type WithoutChild} from "$lib/utils.js";
    import type {HTMLAttributes} from "svelte/elements";

    let {
        class: className,
        children,
        ...restProps
    }: WithoutChild<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div
    class={cn("flex items-center gap-3", className)}
    {...restProps}
>
    <Separator class="flex-1" />
    {#if children}
        <div class="text-sm font-medium text-muted-foreground shrink-0">
            {@render children()}
        </div>
    {/if}
    <Separator class="flex-1" />
</div>
