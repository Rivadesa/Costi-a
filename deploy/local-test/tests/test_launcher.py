"""Exercise the actual CMD control flow on Windows using harmless Docker/curl stubs.
This checks orchestration, not Docker Desktop itself. Real HTTP is tested separately.
"""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


@unittest.skipUnless(os.name == "nt", "CMD execution requires Windows")
class LauncherTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="hos launcher ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.log = self.root / "commands.txt"
        for executable in ("docker", "curl"):
            (self.bin / (executable + ".cmd")).write_text(
                "@echo off\n"
                "echo " + executable + ' %*>>"%HOS_COMMAND_LOG%"\n'
                'if "%HOS_FAIL_INFO%"=="1" if "%1"=="info" exit /b 1\n'
                'if "%HOS_FAIL_BUILD%"=="1" if "%4"=="build" exit /b 1\n'
                "exit /b 0\n", encoding="ascii")
        self.env = dict(os.environ)
        self.env.update(PATH=str(self.bin) + os.pathsep + os.environ["PATH"], HOS_COMMAND_LOG=str(self.log))
        # Also test extraction into a path containing spaces, without touching real Docker.
        self.package = self.root / "package with spaces"
        deploy = self.package / "deploy" / "local-test"
        deploy.mkdir(parents=True)
        source = Path(__file__).resolve().parents[1] / "test-server.cmd"
        self.launcher = deploy / source.name
        self.launcher.write_bytes(source.read_bytes())
        (deploy / "docker-compose.yml").write_text("services: {}\n", encoding="ascii")

    def run_action(self, action):
        result = subprocess.run([os.environ.get("COMSPEC", "cmd.exe"), "/d", "/c", str(self.launcher), action],
                                cwd=self.bin, env=self.env, capture_output=True, text=True, timeout=20)
        commands = self.log.read_text() if self.log.exists() else ""
        self.assertNotIn("down -v", commands)
        self.assertNotIn("migrate:fresh", commands)
        return result, commands

    def test_start_has_no_initialization_or_download(self):
        result, commands = self.run_action("start")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("up -d --no-build --pull never", commands)
        self.assertIn("http://127.0.0.1:8000/api/v1/meta", commands)
        self.assertNotIn("backend init", commands)
        self.assertNotIn("build backend", commands)

    def test_init_is_explicit_and_sequenced(self):
        result, commands = self.run_action("init")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertLess(commands.index("build backend"), commands.index("backend init"))
        self.assertLess(commands.index("backend init"), commands.index("up -d --no-build"))

    def test_build_failure_does_not_initialize_or_start(self):
        self.env["HOS_FAIL_BUILD"] = "1"
        result, commands = self.run_action("init")
        self.assertNotEqual(0, result.returncode)
        self.assertNotIn("backend init", commands)
        self.assertNotIn("up -d", commands)

    def test_missing_daemon_aborts(self):
        self.env["HOS_FAIL_INFO"] = "1"
        result, commands = self.run_action("start")
        self.assertNotEqual(0, result.returncode)
        self.assertNotIn("up -d", commands)

    def test_stop_preserves_containers_and_data(self):
        result, commands = self.run_action("stop")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn('docker-compose.yml" stop', commands)
        self.assertNotIn("down", commands)
        self.assertNotIn("rm", commands)

    def test_unknown_action_does_nothing(self):
        result, commands = self.run_action("erase")
        self.assertEqual(64, result.returncode)
        self.assertEqual("", commands)


if __name__ == "__main__":
    unittest.main(verbosity=2)
