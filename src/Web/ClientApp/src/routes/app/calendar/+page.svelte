<script lang="ts">
    import Timetable from "$lib/components/timetable/timetable.svelte";
    import type {TimetableEvent} from "$lib/timetable/types";
    import {Button} from "$lib/components/ui/button";
    import {Plus} from "@lucide/svelte";

    const events: TimetableEvent[] = [];
</script>

<Timetable
    events={events}
    startHour={8}
    endHour={23}
    hourHeight={80}
/>
<!--TODO: добавить кнопку Сегодня которая показывается если переключиться на другие дни от сегодня и переводит на сегодня-->
<Button
    class="fixed right-4 bottom-18 z-50 rounded-full shadow-lg"
    size="icon"
    aria-label="Создать новое событие"
>
    <!--    onclick={openCreateSongDialog}-->
    <Plus/>
</Button>
