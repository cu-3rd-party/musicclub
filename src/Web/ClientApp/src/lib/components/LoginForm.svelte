<script lang="ts">
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import {
        FieldGroup,
        Field,
        FieldLabel,
        FieldDescription,
        FieldSeparator,
    } from "$lib/components/ui/field/index.js";
    import * as Alert from "$lib/components/ui/alert/index.js";
    import {login} from "$lib/auth/store";
    import {getApiErrorMessage} from "$lib/api/auth";
    import {Input} from "$lib/components/ui/input/index.js";
    import {Button} from "$lib/components/ui/button/index.js";
    import {cn, type WithElementRef} from "$lib/utils.js";
    import type {HTMLFormAttributes} from "svelte/elements";

    let {
        ref = $bindable(null),
        class: className,
        ...restProps
    }: WithElementRef<HTMLFormAttributes> = $props();

    const id = $props.id();
    let email = $state("");
    let password = $state("");
    let errorMessage = $state("");
    let isSubmitting = $state(false);

    async function handleSubmit(event: SubmitEvent): Promise<void> {
        event.preventDefault();
        if (isSubmitting) {
            return;
        }

        errorMessage = "";
        isSubmitting = true;

        try {
            await login({email, password});
            await goto(resolve("/"));
        } catch (error) {
            errorMessage = getApiErrorMessage(error, "Не удалось выполнить вход.");
        } finally {
            isSubmitting = false;
        }
    }
</script>

<form class={cn("flex flex-col gap-6", className)} bind:this={ref} onsubmit={handleSubmit} {...restProps}>
    <FieldGroup>
        <div class="flex flex-col items-center gap-1 text-center">
            <h1 class="text-2xl font-bold">Войти в ваш аккаунт</h1>
        </div>
        {#if errorMessage}
            <Alert.Root variant="destructive">
                <Alert.Description>{errorMessage}</Alert.Description>
            </Alert.Root>
        {/if}
        <Field>
            <FieldLabel for="email-{id}">Почта</FieldLabel>
            <Input id="email-{id}" type="email" placeholder="potanin@nornickel.ru" bind:value={email} required/>
        </Field>
        <Field>
            <div class="flex items-center">
                <FieldLabel for="password-{id}">Пароль</FieldLabel>
                <a href={resolve('/auth/reset')} class="ms-auto text-sm underline-offset-4 hover:underline">
                    <!-- TODO -->
                    Забыли ваш пароль?
                </a>
            </div>
            <Input id="password-{id}" type="password" bind:value={password} required/>
        </Field>
        <Field>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? "Входим..." : "Продолжить"}</Button>
        </Field>
        <FieldSeparator>Или можете продолжить через</FieldSeparator>
        <Field>
            <FieldDescription class="text-center">
                Нет аккаунта?
                <a href={resolve('/auth/signup')} class="underline underline-offset-4">Зарегистрироваться</a>
            </FieldDescription>
        </Field>
    </FieldGroup>
</form>
