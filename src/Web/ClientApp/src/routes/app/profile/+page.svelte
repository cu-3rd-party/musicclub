<script lang="ts">
    import {Avatar, AvatarFallback, AvatarImage} from "$lib/components/ui/avatar";
    import {Badge} from "$lib/components/ui/badge";
    import {Button} from "$lib/components/ui/button";
    import {Separator, SeparatorWithLabel} from "$lib/components/ui/separator";
    import {authState} from "$lib/auth/store";
    import {Shield} from "@lucide/svelte";
    import * as Sheet from "$lib/components/ui/sheet";
    import {categorizePermissions, PermissionCategory, Permission} from "$lib/permissions/resolve";
    import * as Alert from "$lib/components/ui/alert";
    import {getMyAssignments, getMyRoadieAssignments} from "$lib/api/users";
    import type {SongRoleAssignment, ShortSongDto} from "$lib/songs/types";
    import SongRow from "$lib/components/songs/song-row.svelte";
    import {Skeleton} from "$lib/components/ui/skeleton";
    import {onMount} from "svelte";

    let error = $state<string | null>(null);
    let permissionsOpen = $state(false);
    let detailsOpen = $state(false);

    let assignments = $state<SongRoleAssignment[]>([]);
    let assignmentsLoading = $state(true);

    let roadieAssignments = $state<ShortSongDto[]>([]);
    let roadieAssignmentsLoading = $state(true);

    let user = $derived($authState.user);
    let permissionCategory = $derived($authState.user ? categorizePermissions($authState.user.permissions) : PermissionCategory.None);
    let hasRoadieManage = $derived(user?.permissions?.includes(Permission.RoadieManage) ?? false);

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

    onMount(async () => {
        getMyAssignments()
            .then((data) => { assignments = data; })
            .catch((err) => console.error(err))
            .finally(() => { assignmentsLoading = false; });

        if (hasRoadieManage) {
            getMyRoadieAssignments()
                .then((data) => { roadieAssignments = data; })
                .catch((err) => console.error(err))
                .finally(() => { roadieAssignmentsLoading = false; });
        } else {
            roadieAssignmentsLoading = false;
        }
    });

</script>

<div class="space-y-8 p-4">
    {#if error}
        <Alert.Root variant="destructive">
            <Alert.Description>{error}</Alert.Description>
        </Alert.Root>
    {/if}
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

    <SeparatorWithLabel>Мои роли</SeparatorWithLabel>

    <section class="space-y-3">

        {#if assignmentsLoading}
            <div class="space-y-3">
                {#each Array.from({length: 3}, (_, i) => i) as i (i)}
                    <div class="flex items-center gap-3 py-2">
                        <Skeleton class="size-10 shrink-0 rounded-full" />
                        <div class="flex-1 space-y-2">
                            <Skeleton class="h-4 w-1/3" />
                            <Skeleton class="h-3 w-1/4" />
                        </div>
                    </div>
                {/each}
            </div>
        {:else if assignments.length === 0}
            <p class="text-sm text-muted-foreground py-2">Нет назначенных ролей</p>
        {:else}
            <div class="flex flex-col">
                {#each assignments as assignment (assignment.roleAssignmentId)}
                    <SongRow
                        songId={assignment.song.id}
                        title={assignment.song.title}
                        artist={assignment.song.artist}
                        imageUrl={assignment.song.thumbnailUrl}
                        roleTitle={assignment.title}
                    />
                {/each}
            </div>
        {/if}
    </section>

    {#if hasRoadieManage}
    <SeparatorWithLabel>Роди</SeparatorWithLabel>

    <section class="space-y-3">

        {#if roadieAssignmentsLoading}
            <div class="space-y-3">
                {#each Array.from({length: 3}, (_, i) => i) as i (i)}
                    <div class="flex items-center gap-3 py-2">
                        <Skeleton class="size-10 shrink-0 rounded-full" />
                        <div class="flex-1 space-y-2">
                            <Skeleton class="h-4 w-1/3" />
                            <Skeleton class="h-3 w-1/4" />
                        </div>
                    </div>
                {/each}
            </div>
        {:else if roadieAssignments.length === 0}
            <p class="text-sm text-muted-foreground py-2">Нет назначенных роди</p>
        {:else}
            <div class="flex flex-col">
                {#each roadieAssignments as song (song.id)}
                    <SongRow
                        songId={song.id}
                        title={song.title}
                        artist={song.artist}
                        imageUrl={song.thumbnailUrl}
                    />
                {/each}
            </div>
        {/if}
    </section>
    {/if}

<!--    <section class="space-y-4">-->
<!--        <h2 class="text-sm font-medium text-muted-foreground">-->
<!--            Приватность-->
<!--        </h2>-->

<!--        <div class="flex items-center justify-between gap-4">-->
<!--            <div class="space-y-0.5">-->
<!--                <p class="text-sm font-medium">Разрешить добавлять меня на роли</p>-->
<!--                <p class="text-xs text-muted-foreground">-->
<!--                    Другие участники смогут назначать вас на роли в песнях без вашего участия-->
<!--                </p>-->
<!--            </div>-->
<!--            <Switch-->
<!--                checked={preferences.allowAdding}-->
<!--                disabled={preferencesLoading}-->
<!--                onCheckedChange={(checked) => togglePreference("allowAdding", checked)}-->
<!--            />-->
<!--        </div>-->

<!--        <div class="flex items-center justify-between gap-4">-->
<!--            <div class="space-y-0.5">-->
<!--                <p class="text-sm font-medium">Разрешить снимать меня с ролей</p>-->
<!--                <p class="text-xs text-muted-foreground">-->
<!--                    Другие участники смогут убирать вас с ролей в песнях без вашего участия-->
<!--                </p>-->
<!--            </div>-->
<!--            <Switch-->
<!--                checked={preferences.allowRemoving}-->
<!--                disabled={preferencesLoading}-->
<!--                onCheckedChange={(checked) => togglePreference("allowRemoving", checked)}-->
<!--            />-->
<!--        </div>-->
<!--    </section>-->

<!--    <Separator />-->

<!--    <section>-->
<!--        <Button-->
<!--            variant="destructive"-->
<!--            class="w-full sm:w-auto"-->
<!--            onclick={handleLogout}-->
<!--            disabled={loggingOut}-->
<!--        >-->
<!--            <LogOut class="mr-2 size-4" />-->
<!--            {loggingOut ? "Выход..." : "Выйти из аккаунта"}-->
<!--        </Button>-->
<!--    </section>-->
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
