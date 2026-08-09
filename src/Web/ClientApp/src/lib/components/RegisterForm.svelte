<script lang="ts">
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import * as Alert from "$lib/components/ui/alert/index.js";
    import {getApiErrorMessage} from "$lib/api/auth";
    import {signUp} from "$lib/auth/store";
    import {cn} from "$lib/utils.js";
    import {Button} from "$lib/components/ui/button/index.js";
    import * as Field from "$lib/components/ui/field/index.js";
    import {Input} from "$lib/components/ui/input/index.js";
    import type {HTMLAttributes} from "svelte/elements";

    let {class: className, ...restProps}: HTMLAttributes<HTMLFormElement> = $props();
    let name = $state("");
    let email = $state("");
    let password = $state("");
    let confirmPassword = $state("");
    let errorMessage = $state("");
    let isSubmitting = $state(false);

    async function handleSubmit(event: SubmitEvent): Promise<void> {
        event.preventDefault();
        if (isSubmitting) {
            return;
        }

        if (password !== confirmPassword) {
            errorMessage = "Пароли не совпадают.";
            return;
        }

        errorMessage = "";
        isSubmitting = true;

        try {
            await signUp({name, email, password});
            await goto(resolve("/"));
        } catch (error) {
            errorMessage = getApiErrorMessage(error, "Не удалось создать аккаунт.");
        } finally {
            isSubmitting = false;
        }
    }
</script>

<form class={cn("flex flex-col gap-6", className)} onsubmit={handleSubmit} {...restProps}>
    <Field.Group>
        <div class="flex flex-col items-center gap-1 text-center">
            <h1 class="text-2xl font-bold">Создайте ваш аккаунт</h1>
        </div>
        {#if errorMessage}
            <Alert.Root variant="destructive">
                <Alert.Description>{errorMessage}</Alert.Description>
            </Alert.Root>
        {/if}
        <Field.Field>
            <Field.Label for="name">Полное имя</Field.Label>
            <Input id="name" type="text" placeholder="Владимир Потанин" bind:value={name} required/>
        </Field.Field>
        <Field.Field>
            <Field.Label for="email">Почта</Field.Label>
            <Input id="email" type="email" placeholder="potanin@nornickel.ru" bind:value={email} required/>
        </Field.Field>
        <Field.Field>
            <Field.Label for="password">Пароль</Field.Label>
            <Input id="password" type="password" bind:value={password} required/>
            <Field.Description>Должен быть минимум 8 символов.</Field.Description>
        </Field.Field>
        <Field.Field>
            <Field.Label for="confirm-password">Подтверждение пароля</Field.Label>
            <Input id="confirm-password" type="password" bind:value={confirmPassword} required/>
            <Field.Description>Пожалуйста, подтвердите ваш пароль.</Field.Description>
        </Field.Field>
        <Field.Field>
            <Button type="submit"
                    disabled={isSubmitting}>{isSubmitting ? "Создаем аккаунт..." : "Создать аккаунт"}</Button>
        </Field.Field>
        <Field.Separator>Или продолжить через</Field.Separator>
        <Field.Field>
            <Field.Description class="px-6 text-center">
                Уже есть аккаунт? <a href={resolve('/auth')}>Войти</a>
            </Field.Description>
        </Field.Field>
    </Field.Group>
</form>
