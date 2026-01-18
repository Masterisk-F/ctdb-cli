-include config.mk

PREFIX ?= /usr/local
BINDIR = $(PREFIX)/bin
LIBDIR = $(PREFIX)/lib/ctdb-cli
PUBLISH_DIR = bin/publish

.PHONY: all install clean setup

all: setup
	dotnet publish CTDB.CLI/CTDB.CLI.csproj -c Release -o $(PUBLISH_DIR)

setup:
	./setup.sh

install:
	install -d $(DESTDIR)$(LIBDIR)
	cp -r $(PUBLISH_DIR)/* $(DESTDIR)$(LIBDIR)/
	install -d $(DESTDIR)$(BINDIR)
	ln -sf $$(realpath --relative-to=$(BINDIR) $(LIBDIR))/ctdb-cli $(DESTDIR)$(BINDIR)/ctdb-cli


clean:
	dotnet clean
	rm -rf $(PUBLISH_DIR)
	rm -rf CTDB.CLI/bin CTDB.CLI/obj
	rm -f config.mk
