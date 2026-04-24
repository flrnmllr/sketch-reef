#!/bin/bash

source .venv/bin/activate
pyinstaller --onefile --add-data "../app-frontend/dist:vue-app" app.py