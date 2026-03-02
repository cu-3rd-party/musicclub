import { describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import ErrorBoundary from "./ErrorBoundary.vue";

describe("ErrorBoundary", () => {
  it("renders slot content when no error", () => {
    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: "<div class='slot-ok'>ok</div>",
      },
    });

    expect(wrapper.find(".slot-ok").exists()).toBe(true);
    expect(wrapper.find(".error-boundary__card").exists()).toBe(false);
  });

  it("captures errors from child components", async () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
    const Thrower = defineComponent({
      name: "Thrower",
      render() {
        throw new Error("Boom");
      },
    });

    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: h(Thrower),
      },
    });

    await nextTick();

    expect(wrapper.text()).toContain("Ошибка приложения");
    expect(wrapper.text()).toContain("Boom");
    errorSpy.mockRestore();
  });
});
