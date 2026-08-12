<script lang="ts">
    import {
        FieldGroup,
        Field,
        FieldLabel,
    } from "$lib/components/ui/field";
    import {Input} from "$lib/components/ui/input";
    import {Button} from "$lib/components/ui/button";
    import {cn, type WithElementRef} from "$lib/utils.js";
    import type {HTMLFormAttributes} from "svelte/elements";

    let {
        ref = $bindable(null),
        class: className,
        ...restProps
    }: WithElementRef<HTMLFormAttributes> = $props();

    const id = $props.id();
</script>

<form class={cn("flex flex-col gap-6", className)} bind:this={ref} {...restProps}>
    <FieldGroup>
        <div class="flex flex-col items-center gap-1 text-center">
            <h1 class="text-2xl font-bold">Восстановить пароль</h1>
        </div>
        <Field>
            <FieldLabel for="email-{id}">Почта</FieldLabel>
            <Input id="email-{id}" type="email" placeholder="potanin@nornickel.ru" required/>
        </Field>
        <Field>
            <Button type="submit">Продолжить</Button>
        </Field>
    </FieldGroup>
</form>
