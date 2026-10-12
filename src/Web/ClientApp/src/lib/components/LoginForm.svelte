<script lang="ts">
    import {LogIn} from "@lucide/svelte";
    import {FieldGroup, Field} from "$lib/components/ui/field";
    import * as Alert from "$lib/components/ui/alert";
    import {externalLoginUrl} from "$lib/api/auth";
    import {Button} from "$lib/components/ui/button";
    import {cn, type WithElementRef} from "$lib/utils.js";
    import type {HTMLAttributes} from "svelte/elements";
    import * as Card from "$lib/components/ui/card";
    import type {ExternalProvider} from "$lib/auth/types";
    import {DEFAULT_APP_PAGE} from "$lib/config";

    let {
        ref = $bindable(null),
        class: className,
        providers,
        error = null,
        ...restProps
    }: WithElementRef<HTMLAttributes<HTMLDivElement>> & {
        providers: ExternalProvider[];
        error?: string | null;
    } = $props();

    const errorMessage = $derived(
        error ? "Не удалось войти. Попробуйте ещё раз." : "",
    );
    // Абсолютный путь: resolve() здесь даёт относительный (./app/songs), а бэкенд принимает только /…
    const returnUrl = DEFAULT_APP_PAGE;
</script>

<div class={cn("flex flex-col gap-6", className)} bind:this={ref} {...restProps}>
    <Card.Root>
        <Card.Content>
            <FieldGroup>
                <div class="flex flex-col items-center gap-1 text-center">
                    <h1 class="text-2xl font-bold">Войти в систему</h1>
                </div>
                {#if errorMessage}
                    <Alert.Root variant="destructive">
                        <Alert.Description>{errorMessage}</Alert.Description>
                    </Alert.Root>
                {/if}
                <Field>
                    {#each providers as provider (provider.name)}
                        <Button
                            variant="outline"
                            href={externalLoginUrl(provider.name, returnUrl)}
                            data-sveltekit-reload
                        >
                            {#if provider.name === "telegram"}
                                <svg width="800px" height="800px" viewBox="0 0 48 48" fill="none"
                                     xmlns="http://www.w3.org/2000/svg">
                                    <path
                                        d="M41.4193 7.30899C41.4193 7.30899 45.3046 5.79399 44.9808 9.47328C44.8729 10.9883 43.9016 16.2908 43.1461 22.0262L40.5559 39.0159C40.5559 39.0159 40.3401 41.5048 38.3974 41.9377C36.4547 42.3705 33.5408 40.4227 33.0011 39.9898C32.5694 39.6652 24.9068 34.7955 22.2086 32.4148C21.4531 31.7655 20.5897 30.4669 22.3165 28.9519L33.6487 18.1305C34.9438 16.8319 36.2389 13.8019 30.8426 17.4812L15.7331 27.7616C15.7331 27.7616 14.0063 28.8437 10.7686 27.8698L3.75342 25.7055C3.75342 25.7055 1.16321 24.0823 5.58815 22.459C16.3807 17.3729 29.6555 12.1786 41.4193 7.30899Z"
                                        fill="#FFFFFF"/>
                                </svg>
                            {:else}
                                <LogIn/>
                            {/if}
                            Войти через {provider.displayName}
                        </Button>
                    {:else}
                        <p class="text-center text-sm text-muted-foreground">
                            Вход временно недоступен: не настроен ни один провайдер.
                        </p>
                    {/each}
                </Field>
            </FieldGroup>
        </Card.Content>
    </Card.Root>
</div>
