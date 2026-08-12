<script lang="ts">
    import BottomNav from "$lib/components/bottom-nav.svelte";
    import CalendarIcon from "@lucide/svelte/icons/calendar";
    import MusicIcon from "@lucide/svelte/icons/music";
    import UserIcon from "@lucide/svelte/icons/user";

    let {children} = $props();

    const navItems = [
        {label: "Песни", href: "/app/songs", icon: MusicIcon},
        {label: "Календарь", href: "/app/calendar", icon: CalendarIcon},
        {label: "Профиль", href: "/app/profile", icon: UserIcon},
    ];
</script>

<div class="flex min-h-screen flex-col">
    <main class="flex-1 overflow-y-auto pb-16">
        {@render children?.()}
    </main>
    <BottomNav items={navItems} />
</div>
