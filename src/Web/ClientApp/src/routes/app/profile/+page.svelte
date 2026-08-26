<script lang="ts">
    import {Avatar, AvatarFallback, AvatarImage} from "$lib/components/ui/avatar";
    import {Badge} from "$lib/components/ui/badge";
    import {Button} from "$lib/components/ui/button";
    import {Separator} from "$lib/components/ui/separator";
    import {authState, logout} from "$lib/auth/store";
    import {getApiErrorMessage} from "$lib/api/auth";
    import {LogOut, Shield} from "@lucide/svelte";
    import * as Sheet from "$lib/components/ui/sheet";
    import {resolve} from "$app/paths";
    import {goto} from "$app/navigation";
    import {categorizePermissions, PermissionCategory} from "$lib/permissions/resolve";

    let error = $state<string | null>(null);
    let loggingOut = $state(false);
    let permissionsOpen = $state(false);
    let detailsOpen = $state(false);

    let user = $derived($authState.user);
    let permissionCategory = $derived($authState.user ? categorizePermissions($authState.user.permissions) : PermissionCategory.None);

    function formatDate(dateString: string): string {
        if (typeof dateString == "undefined") {
            return "???";
        }
        try {
            return new Date(dateString).toLocaleDateString("ru-RU", {
                year: "numeric",
                month: "long",
                day: "numeric",
            });
        } catch {
            return dateString;
        }
    }

    function formatDateTime(dateString: string): string {
        try {
            return new Date(dateString).toLocaleString("ru-RU", {
                year: "numeric",
                month: "long",
                day: "numeric",
                hour: "2-digit",
                minute: "2-digit",
            });
        } catch {
            return dateString;
        }
    }

    function getInitials(name: string | undefined): string {
        if (!name) return "?";
        const parts = name.trim().split(/\s+/);
        if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
        return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
    }

    async function handleLogout() {
        loggingOut = true;
        try {
            await logout();
            await goto(resolve("/auth"))
        } catch (err) {
            error = getApiErrorMessage(err, "Не удалось выйти");
        } finally {
            loggingOut = false;
        }
    }

</script>

<div class="space-y-8 p-4">
    <section class="flex items-start justify-between gap-4">
        <div class="flex min-w-0 items-center gap-4">
            <Avatar class="size-16">
                {#if user?.avatarUrl}
                    <AvatarImage
                        src={user?.avatarUrl}
                        alt={user?.displayName}
                    />
                {/if}

                <AvatarFallback class="text-lg">
                    {getInitials(user?.displayName)}
                </AvatarFallback>
            </Avatar>

            <div class="min-w-0">
                <h1 class="truncate text-2xl font-semibold tracking-tight">
                    {user?.displayName ?? "Без имени"}
                </h1>

                <p class="truncate text-sm text-muted-foreground">
                    @{user?.username}
                </p>
            </div>
        </div>
    </section>

    <Separator />

    <section class="space-y-1">
        <h2 class="text-sm font-medium text-muted-foreground">
            Аккаунт
        </h2>

        <Button
            variant="ghost"
            onclick={() => (permissionsOpen = true)}
            disabled={!user?.permissions?.length}
        >
            <span class="flex items-center gap-3">
                <Shield class="size-5 text-muted-foreground" />
                <span class="text-sm font-medium">{permissionCategory} доступы</span>
            </span>
        </Button>
    </section>

    <Separator />

    <section>
        <Button
            variant="destructive"
            class="w-full sm:w-auto"
            onclick={handleLogout}
            disabled={loggingOut}
        >
            <LogOut class="mr-2 size-4" />
            {loggingOut ? "Выход..." : "Выйти из аккаунта"}
        </Button>
    </section>
</div>

<!-- Account metadata lives here, not on the main page -->
<Sheet.Root bind:open={detailsOpen}>
    <Sheet.Content side="right" class="w-full sm:max-w-md">
        <Sheet.Header>
            <Sheet.Title>Account details</Sheet.Title>
            <Sheet.Description>
                Technical information about your account.
            </Sheet.Description>
        </Sheet.Header>

        <div class="mt-6 space-y-5">
            <div class="space-y-1">
                <p class="text-sm text-muted-foreground">Username</p>
                <p class="font-medium">@{user?.username}</p>
            </div>

            <Separator />

            <div class="space-y-1">
                <p class="text-sm text-muted-foreground">Registered</p>
                <p class="font-medium">{user?.createdAt ? formatDate(user?.createdAt) : "???"}</p>
            </div>

            {#if user?.lastLoginAt}
                <Separator />

                <div class="space-y-1">
                    <p class="text-sm text-muted-foreground">Last login</p>
                    <p class="font-medium">{formatDateTime(user?.lastLoginAt)}</p>
                </div>
            {/if}

            <Separator />

            <div class="space-y-1">
                <p class="text-sm text-muted-foreground">Last updated</p>
                <p class="font-medium">{user?.updatedAt ? formatDateTime(user?.updatedAt) : "???"}</p>
            </div>
        </div>
    </Sheet.Content>
</Sheet.Root>

<!-- Permissions as secondary information -->
<Sheet.Root bind:open={permissionsOpen}>
    <Sheet.Content side="right" class="w-full sm:max-w-md">
        <Sheet.Header>
            <Sheet.Title>Permissions</Sheet.Title>
            <Sheet.Description>
                Permissions currently granted to this account.
            </Sheet.Description>
        </Sheet.Header>

        <div class="mt-6 flex flex-wrap gap-2">
            {#each user?.permissions ?? [] as permission (permission)}
                <Badge variant="outline" class="font-mono text-xs">
                    {permission}
                </Badge>
            {/each}
        </div>
    </Sheet.Content>
</Sheet.Root>
