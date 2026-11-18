"""Offline MuJoCo experiment; no DDS or hardware connection is created."""
import argparse
import math
from pathlib import Path
import subprocess
import sys
import time

import mujoco
import numpy as np


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--simulator-root", type=Path, required=True)
    parser.add_argument("--controller", type=Path, required=True)
    parser.add_argument("--view", action="store_true")
    args = parser.parse_args()

    root = args.simulator_root.resolve()
    sys.path.insert(0, str(root / "simulate"))
    import config
    from bumi_dds_bridge import HighLevelPolicy

    spec = mujoco.MjSpec.from_file(str(root / "bumi_robot" / "scene.xml"))
    marker = spec.worldbody.add_body(name="follow_target", mocap=True, pos=[3.2, 0, 0.63])
    marker.add_geom(name="follow_target_shape", type=mujoco.mjtGeom.mjGEOM_CAPSULE,
                    size=[0.18, 0.45, 0], rgba=[1, 0.45, 0.1, 1], contype=0, conaffinity=0)
    model = spec.compile()
    model.opt.timestep = config.SIMULATE_DT
    data = mujoco.MjData(model)
    mujoco.mj_resetDataKeyframe(model, data, 0)
    mujoco.mj_forward(model, data)
    marker_id = mujoco.mj_name2id(model, mujoco.mjtObj.mjOBJ_BODY, "follow_target")
    marker_index = model.body_mocapid[marker_id]
    policy = HighLevelPolicy()

    process = subprocess.Popen(
        [str(args.controller.resolve()), "--offline-simulation"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, bufsize=1,
    )
    viewer = None
    minimum = float("inf")
    max_lost_command = 0.0
    max_near_command = 0.0
    near_count = 0
    lost_count = 0
    max_tracking_error = 0.0
    loss_start = None
    loss_displacement = 0.0
    moving_base_distance = 0.0
    try:
        if args.view:
            from mujoco import viewer as mj_viewer
            viewer = mj_viewer.launch_passive(model, data)
            viewer.cam.distance = 6
            viewer.cam.elevation = -20
            viewer.cam.azimuth = 90

        forward = yaw_command = 0.0
        initial_distance = 3.2
        last_distance = initial_distance
        started = time.monotonic()
        for step in range(15000):
            elapsed = step * config.SIMULATE_DT
            # The target walks, then approaches the robot briefly, then stops.
            target_x = 3.2 + 0.12 * min(elapsed, 6) - 0.38 * min(max(elapsed - 6, 0), 3)
            target_y = 0.0
            data.mocap_pos[marker_index] = [target_x, target_y, 0.63]
            robot_x, robot_y = data.qpos[:2]
            w, x, y, z = data.qpos[3:7]
            yaw = math.atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z))
            actual_distance = math.hypot(target_x - robot_x, target_y - robot_y)
            minimum = min(minimum, actual_distance)
            visible = not (10 <= elapsed < 12)
            if elapsed < 6:
                moving_base_distance = max(moving_base_distance, math.hypot(robot_x, robot_y))

            if step % 20 == 0:
                process.stdin.write(
                    f"{robot_x:.12g},{robot_y:.12g},{yaw:.12g},"
                    f"{target_x:.12g},{target_y:.12g},{str(visible).lower()},0.02\n"
                )
                process.stdin.flush()
                response = process.stdout.readline()
                if not response:
                    raise RuntimeError("Controller exited without a response")
                fields = response.strip().split(",")
                if len(fields) != 4:
                    raise RuntimeError(f"Invalid controller response: {response!r}")
                forward, yaw_command, reported_distance = map(float, fields[:3])
                if not all(map(math.isfinite, (forward, yaw_command, reported_distance))):
                    raise RuntimeError("Controller returned a non-finite value")
                max_tracking_error = max(max_tracking_error, abs(actual_distance - reported_distance))
                if not visible:
                    lost_count += 1
                    max_lost_command = max(max_lost_command, abs(forward), abs(yaw_command))
                    if loss_start is None:
                        loss_start = (robot_x, robot_y)
                    loss_displacement = max(loss_displacement, math.hypot(
                        robot_x - loss_start[0], robot_y - loss_start[1]))
                if actual_distance <= 2.0:
                    near_count += 1
                    max_near_command = max(max_near_command, abs(forward), abs(yaw_command))
                last_distance = actual_distance

            data.ctrl[:] = policy.step(data.sensordata, forward, 0.0, yaw_command)
            mujoco.mj_step(model, data)
            if not np.isfinite(data.qpos).all():
                raise RuntimeError("Non-finite MuJoCo state")
            if viewer and step % 20 == 0:
                if not viewer.is_running():
                    raise RuntimeError("Viewer closed before simulation completed")
                viewer.cam.lookat[:] = [(robot_x + target_x) / 2, (robot_y + target_y) / 2, 0.6]
                viewer.sync()
            if viewer:
                delay = started + elapsed - time.monotonic()
                if delay > 0:
                    time.sleep(delay)

        if minimum < 1.5:
            raise RuntimeError(f"Simulated center distance below 1.5 m: {minimum:.3f}")
        if max_tracking_error > 1e-6:
            raise RuntimeError(f"Controller distance differs from scene: {max_tracking_error:.9f}")
        if lost_count == 0 or max_lost_command != 0:
            raise RuntimeError("Target-loss commands were not zero")
        if near_count == 0 or max_near_command != 0:
            raise RuntimeError(f"Stop-distance check failed: samples={near_count}, command={max_near_command:.3f}")
        if last_distance >= initial_distance:
            raise RuntimeError(
                f"Robot did not reduce the distance to the target: "
                f"initial={initial_distance:.3f}m final={last_distance:.3f}m "
                f"min={minimum:.3f}m base=({data.qpos[0]:.3f},{data.qpos[1]:.3f})"
            )
        if moving_base_distance < 0.3:
            raise RuntimeError(f"Robot did not follow the moving target: {moving_base_distance:.3f}m")
        print(f"PASS: actual center distance min={minimum:.3f}m final={last_distance:.3f}m; "
              f"position error={max_tracking_error:.9f}m; lost frames={lost_count}, "
              f"lost command max={max_lost_command:.3f}; "
              f"stop-distance frames={near_count}; following displacement={moving_base_distance:.3f}m; "
              f"loss-period base displacement={loss_displacement:.3f}m")
    finally:
        process.stdin.close()
        try:
            return_code = process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
            raise RuntimeError("Controller did not exit after input closed")
        if viewer:
            viewer.close()
        if return_code != 0:
            raise RuntimeError(f"Controller exited with status {return_code}")


if __name__ == "__main__":
    main()
