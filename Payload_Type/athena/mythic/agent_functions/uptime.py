from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class UptimeArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class UptimeCommand(CommandBase):
    cmd = "uptime"
    needs_admin = False
    help_cmd = "uptime"
    description = "output the current uptime in D:H:M:S"
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = UptimeArguments

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
