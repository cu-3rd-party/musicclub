LOCALES_PO := $(wildcard locales/*/LC_MESSAGES/*.po)
LOCALES_MO := $(LOCALES_PO:.po=.mo)

.PHONY: compile-locales

compile-locales: $(LOCALES_MO)
	@echo "Compiled $(words $(LOCALES_MO)) locale file(s)."

%.mo: %.po
	msgfmt $< -o $@
