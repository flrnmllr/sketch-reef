from flask import Flask, request, jsonify, send_from_directory
from werkzeug.utils import secure_filename
import os
import sys
import time
import threading

from watchdog.events import FileSystemEvent, FileSystemEventHandler
from watchdog.observers import Observer

from sketch_scanner import SketchScanner
sketch_scanner = SketchScanner()

app = Flask(__name__, static_folder="vue-app", static_url_path="")

def get_base_dir():
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    if hasattr(sys, "_MEIPASS"):
        return sys._MEIPASS
    return os.path.abspath(".")

BASE_DIR = get_base_dir()

app.config["APP_FOLDER"] = os.path.join(BASE_DIR, "app-data")
app.config["UPLOAD_FOLDER"] = os.path.join(app.config["APP_FOLDER"], "uploads")
app.config["SKETCH_FOLDER"] = os.path.join(app.config["APP_FOLDER"], "sketches")
app.config["WATCH_FOLDER"] = os.path.join(app.config["APP_FOLDER"], "photos")

ALLOWED_EXTENSIONS = {".pdf", ".png", ".jpg", ".jpeg"}

def wait_until_complete(path):
    size = -1
    while True:
        new_size = os.path.getsize(path)
        if new_size == size:
            return
        size = new_size
        time.sleep(1)

def allowed_file(filename):
    _, ext = os.path.splitext(filename)
    return ext.lower() in ALLOWED_EXTENSIONS

class EventHandler(FileSystemEventHandler):
    def on_created(self, event):
        if event.is_directory:
            return
        _, ext = os.path.splitext(event.src_path)
        if ext.lower() in ALLOWED_EXTENSIONS:
            wait_until_complete(event.src_path)
            try:
                sketch_scanner.scan(event.src_path, app.config["SKETCH_FOLDER"])
            except Exception as e:
                print(f"[Watchdog] {e}")
            os.remove(event.src_path)

def start_watchdog():
    observer = Observer()
    observer.schedule(EventHandler(), app.config["WATCH_FOLDER"], recursive=False)
    observer.start()
    print("[Watchdog] running...")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        observer.stop()
    observer.join()

@app.route("/", methods=["GET"])
def frontend():
    return send_from_directory(app.static_folder, "index.html")

@app.route("/<path:path>")
def serve_static(path):
    return send_from_directory(app.static_folder, path)

@app.route("/api", methods=["POST"])
def upload_file():
    if "file" not in request.files:
        return jsonify({"success": False, "error": "No file part"}), 400

    file = request.files["file"]

    if file.filename == "":
        return jsonify({"success": False, "error": "Empty filename"}), 400

    if not "." in file.filename and os.path.splitext(file.filename).lower() in ALLOWED_EXTENSIONS:
        return jsonify({"success": False, "error": "File type not allowed"}), 400

    filename = secure_filename(file.filename)
    file_path = os.path.join(app.config["UPLOAD_FOLDER"], filename)

    try:
        file.save(file_path)

        sketch_scanner.scan(file_path, app.config["SKETCH_FOLDER"])

        os.remove(file_path)

        return jsonify({
            "success": True
        }), 200

    except Exception as e:
        os.remove(file_path)
        return jsonify({
            "success": False,
            "error": str(e)
        }), 500

@app.route("/api", methods=["GET"])
def get_sketches():
    try:
        files = os.listdir(app.config["SKETCH_FOLDER"])
        current_time = time.time()
        data = {
            "sketches": []
        }
        for i, filename in enumerate(files):
            file_path = os.path.join(app.config["SKETCH_FOLDER"], filename)
            file_mtime = os.path.getmtime(file_path)
            if True or current_time - file_mtime <= 60:
                if filename.count("_") >= 1:
                    parts = filename.split("_")
                    if len(parts) == 2:
                        timestamp = parts[0]
                        identifier = parts[1].split(".")[0]
                        data["sketches"].append({
                            "timestamp": timestamp,
                            "identifier": identifier,
                            "filename": filename
                        })
        return jsonify(data)
    except FileNotFoundError:
        return jsonify(), 404

@app.route("/api/<filename>", methods=["GET"])
def get_sketch(filename):
    return send_from_directory(app.config["SKETCH_FOLDER"], filename)

@app.route("/shutdown", methods=["POST"])
def shutdown():
    import os
    import signal

    os.kill(os.getpid(), signal.SIGTERM)
    return "Server shutting down..."

if __name__ == "__main__":

    os.makedirs(app.config["APP_FOLDER"], exist_ok=True)
    os.makedirs(app.config["UPLOAD_FOLDER"], exist_ok=True)
    os.makedirs(app.config["SKETCH_FOLDER"], exist_ok=True)
    os.makedirs(app.config["WATCH_FOLDER"], exist_ok=True)

    threading.Thread(target=start_watchdog, daemon=True).start()

    app.run(host="127.0.0.1", port=5000, use_reloader=False)