#!/bin/bash
set -e
set -u
set -x

docker build --file ./Synchronization.API/Dockerfile --tag ghcr.io/nmshd/bkb-synchronization:${TAG-temp} .
