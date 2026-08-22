<script lang="ts">
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import * as Avatar from "$lib/components/ui/avatar";
    import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
    import * as Sidebar from "$lib/components/ui/sidebar";
    import {logout} from "$lib/auth/store";
    import {useSidebar} from "$lib/components/ui/sidebar";
    import {BadgeCheckIcon, ChevronsUpDownIcon, LogOutIcon} from "@lucide/svelte";

    let {
        user,
    }: {
        user: {
            name: string;
            email: string;
            avatar?: string | null;
        };
    } = $props();

    const sidebar = useSidebar();
    let isLoggingOut = $state(false);

    const avatarFallback = $derived(
        user.name
            .split(" ")
            .filter(Boolean)
            .slice(0, 2)
            .map((part) => part[0]?.toUpperCase() ?? "")
            .join("") || user.email.slice(0, 2).toUpperCase(),
    );

    async function handleLogout(): Promise<void> {
        if (isLoggingOut) {
            return;
        }

        isLoggingOut = true;
        await logout();
        await goto(resolve("/auth"));
        isLoggingOut = false;
    }
</script>

<Sidebar.Menu>
    <Sidebar.MenuItem>
        <DropdownMenu.Root>
            <DropdownMenu.Trigger>
                {#snippet child({props})}
                    <Sidebar.MenuButton
                        {...props}
                        size="lg"
                        class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
                    >
                        <Avatar.Root class="size-8 rounded-lg">
                            <Avatar.Image src={user.avatar ?? undefined} alt={user.name}/>
                            <Avatar.Fallback class="rounded-lg">{avatarFallback}</Avatar.Fallback>
                        </Avatar.Root>
                        <div class="grid flex-1 text-start text-sm leading-tight">
                            <span class="truncate font-medium">{user.name}</span>
                            <span class="truncate text-xs">{user.email}</span>
                        </div>
                        <ChevronsUpDownIcon class="ms-auto size-4"/>
                    </Sidebar.MenuButton>
                {/snippet}
            </DropdownMenu.Trigger>
            <DropdownMenu.Content
                class="w-(--bits-dropdown-menu-anchor-width) min-w-56 rounded-lg"
                side={sidebar.isMobile ? "bottom" : "right"}
                align="end"
                sideOffset={4}
            >
                <DropdownMenu.Label class="p-0 font-normal">
                    <div class="flex items-center gap-2 px-1 py-1.5 text-start text-sm">
                        <Avatar.Root class="size-8 rounded-lg">
                            <Avatar.Image src={user.avatar ?? undefined} alt={user.name}/>
                            <Avatar.Fallback class="rounded-lg">{avatarFallback}</Avatar.Fallback>
                        </Avatar.Root>
                        <div class="grid flex-1 text-start text-sm leading-tight">
                            <span class="truncate font-medium">{user.name}</span>
                            <span class="truncate text-xs">{user.email}</span>
                        </div>
                    </div>
                </DropdownMenu.Label>
                <DropdownMenu.Separator/>
                <DropdownMenu.Group>
                    <DropdownMenu.Item onclick={() => goto(resolve("/app/profile"))}>
                        <BadgeCheckIcon/>
                        Аккаунт
                    </DropdownMenu.Item>
                </DropdownMenu.Group>
                <DropdownMenu.Separator/>
                <DropdownMenu.Item disabled={isLoggingOut} onclick={handleLogout}>
                    <LogOutIcon/>
                    {isLoggingOut ? "Выходим..." : "Выйти"}
                </DropdownMenu.Item>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    </Sidebar.MenuItem>
</Sidebar.Menu>
