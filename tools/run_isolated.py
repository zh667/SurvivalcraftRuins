"""Run the mod in an isolated copy of a Survivalcraft runtime (Windows) and wait for the in-game test.

python run_isolated.py --runtime net --mode server --package "packages/[NET]SurvivalcraftRuins2.1.0.netmod"
python run_isolated.py --runtime scapi-1.9.5.1 --package "packages/[API1.9]SurvivalcraftRuins2.1.0.scmod"
python run_isolated.py --runtime net --mode server --package packages/old/SurvivalcraftRuins.netmod --label control   (2.0.0)

Works in the folder this script sits in (E:/projects/SurvivalcraftRuins-test): packages/ holds the mod packages,
smoke/{net,scapi}/RuinsRuntimeSmoke.dll the test mod (tests/RuntimeSmoke, built on the VPS once per runtime).
Each runtime has one fixed folder (FOLDERS), wiped and refilled every run with a copy of the game's binaries and the
user's settings file muted: the user's own install, its mods and worlds are never touched. The game window is hidden, and the user's cursor is released from
the window clip every 0.2 s and after the run (the engine clips it while its hidden window counts as active).
"""
import argparse, ctypes, json, re, shutil, subprocess, sys, time, zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCES = {
    'net': Path(r'D:\下载\x26.07.01.01'),                                  # 联机版 (SurvivalcraftNet 2.4, x26.07.01.01)
    'scapi-1.9.3.1': Path(r'D:\下载\[Windows]SurvivalcraftAPI_1.9.3.1'),   # 插件版
    'scapi-1.9.5.1': Path(r'D:\下载\[Windows]SurvivalcraftAPI_1.9.5.1'),
}
FOLDERS = {'net': 'runtime-net-server', 'scapi-1.9.3.1': 'runtime-scapi-1.9.3.1-local', 'scapi-1.9.5.1': 'runtime-scapi-1.9.5.1-local'}
COPY_SUFFIXES = ('.dll', '.exe', '.json', '.zip', '.js', '.xml', '.scpak')
SKIP_FILES = {'settings.xml', 'modsettings.xml', 'communitycontentcache.xml', 'gpssetting.xml'}


def hide_windows(pid):
    """The multiplayer runtime shows its window whatever STARTUPINFO says; hide every visible window of the process."""
    from ctypes import wintypes
    user32 = ctypes.WinDLL('user32', use_last_error=True)
    enum_proc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user32.EnumWindows.argtypes = [enum_proc, wintypes.LPARAM]
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user32.IsWindowVisible.argtypes = [wintypes.HWND]
    user32.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
    found = []

    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True
    callback = enum_proc(visit)
    user32.EnumWindows(callback, 0)
    for hwnd in found:
        user32.ShowWindow(hwnd, 0)
    return len(found)


def free_cursor():
    try:
        ctypes.windll.user32.ClipCursor(None)
    except Exception:
        pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--runtime', choices=SOURCES, required=True)
    ap.add_argument('--mode', choices=('local', 'server'), default='local')
    ap.add_argument('--package', required=True)
    ap.add_argument('--label', default='')
    ap.add_argument('--limit', type=int, default=960)
    a = ap.parse_args()
    net = a.runtime == 'net'
    if a.mode == 'server' and not net:
        sys.exit('--mode server needs the multiplayer runtime')
    source = SOURCES[a.runtime]
    # One fixed folder per runtime, whatever the mode or package: Windows asks about the firewall once per program
    # path, so a new path for every run meant a new prompt for the user every run.
    runtime = HERE / FOLDERS[a.runtime]
    if runtime.exists():
        shutil.rmtree(runtime)   # fresh worlds and settings every run
    runtime.mkdir(parents=True)
    for f in source.iterdir():
        if f.is_file() and f.suffix.lower() in COPY_SUFFIXES and f.name.lower() not in SKIP_FILES:
            shutil.copy2(f, runtime / f.name)
    for d in ('runtimes', 'OpenAL'):
        if (source / d).exists():
            shutil.copytree(source / d, runtime / d)
    # Muted from the first frame: the user's own settings file with both volumes at 0 (the test's own volume change
    # only comes after the main menu has already played sound).
    settings = Path('Settings.xml') if net else Path('doc') / 'Settings.xml'
    if (source / settings).exists():
        text = (source / settings).read_text(encoding='utf-8-sig')
        text = re.sub(r'(Name="(?:SoundsVolume|MusicVolume)"[^>]*?Value=")[^"]*"', r'\g<1>0"', text)
        (runtime / settings).parent.mkdir(parents=True, exist_ok=True)
        (runtime / settings).write_text(text, encoding='utf-8')
        print('muted settings:', settings, len(re.findall(r'Name="(?:SoundsVolume|MusicVolume)"[^>]*?Value="0"', text)), 'of 2')
    mods = runtime / ('NetMods' if net else 'Mods')
    mods.mkdir()
    package = Path(a.package) if Path(a.package).is_absolute() else HERE / a.package
    shutil.copy2(package, mods / package.name)
    with zipfile.ZipFile(package) as z:
        tested = json.loads(z.read('modinfo.json').decode('utf-8-sig'))
    smoke = HERE / 'smoke' / ('net' if net else 'scapi') / 'RuinsRuntimeSmoke.dll'
    if net:   # Net 2.4 reads only its own modinfo fields, Dependencies as a list
        info = dict(Name='RuinsSmoke', Version='1.0.0', ApiVersion='1.44', Description='Isolated tests only', ScVersion='2.4.40.6',
                    Link='', Author='Tests', PackageName='local.ruinssmoke', Dependencies=[])
    else:
        info = dict(Name='RuinsSmoke', Version='1.0.0', ApiVersion='1.9.3', ScVersion='2.4', PackageName='local.ruinssmoke',
                    Author='Tests', Description='Isolated tests only', LoadOrder=1300, Dependencies={})
    with zipfile.ZipFile(mods / ('RuinsSmoke.netmod' if net else 'RuinsSmoke.scmod'), 'w') as z:
        z.write(smoke, 'RuinsRuntimeSmoke.dll')
        z.writestr('modinfo.json', json.dumps(info))
    (runtime / 'smoke-mode.txt').write_text(a.mode, encoding='ascii')
    print('runtime:', a.runtime, source)
    print('package:', package.name, tested.get('Version'), '| mode:', a.mode, '| label:', a.label or '-')

    res = runtime / 'smoke-result.json'
    si = subprocess.STARTUPINFO(); si.dwFlags |= subprocess.STARTF_USESHOWWINDOW; si.wShowWindow = 0   # hidden window
    proc = subprocess.Popen([str(runtime / 'Survivalcraft.exe')], cwd=runtime, startupinfo=si)
    t0 = time.time()
    try:
        hidden = 0
        while time.time() - t0 < a.limit and not res.exists() and proc.poll() is None:
            hidden += hide_windows(proc.pid); free_cursor(); time.sleep(0.2)
        print('windows hidden:', hidden)
        for _ in range(15):
            free_cursor(); time.sleep(0.2)
    finally:
        if proc.poll() is None:
            proc.kill()
        free_cursor()
    print('elapsed', round(time.time() - t0), 'exit', proc.poll())
    print(res.read_text(encoding='utf-8') if res.exists() else 'NO RESULT')

    logs = sorted([p for p in runtime.rglob('*') if p.is_file() and (p.suffix.lower() in ('.log', '.txt') or p.parent.name == 'Bugs')
                   and p.name != 'smoke-mode.txt'], key=lambda p: p.stat().st_mtime)
    pattern = re.compile(r'\[Ruins\]|Exception|error', re.I)
    for log in logs:
        lines = log.read_text(encoding='utf-8', errors='replace').splitlines()
        hits = [l for l in lines if pattern.search(l)]
        print(f'LOG {log.relative_to(runtime)} ({len(lines)} lines, {len(hits)} matching)')
        for l in hits[:60]:
            print('   ', l[:300])
    try:
        passed = json.loads(res.read_text(encoding='utf-8')).get('passed') is True if res.exists() else False
    except ValueError:
        passed = False
    if not passed:
        sys.exit(f'{a.runtime} {a.mode} {package.name}: FAILED')


if __name__ == '__main__':
    main()
