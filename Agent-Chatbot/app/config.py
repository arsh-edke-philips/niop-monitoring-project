from functools import lru_cache

from pydantic import SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    dotnet_base_url: str = "https://localhost:5111"
    agent_api_key: SecretStr
    copilot_model: str
    github_token: SecretStr | None = None
    copilot_home: str = ".copilot-home"
    http_timeout_seconds: float = 20.0
    log_file: str = "agent.log"
    chat_api_key: SecretStr | None = None
    max_open_sessions: int = 50


@lru_cache
def get_settings() -> Settings:
    return Settings()