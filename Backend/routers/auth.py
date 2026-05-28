from fastapi import Security, HTTPException
from fastapi.security import APIKeyHeader
# Gibt an, dass wir im HTTP-Header nach einem Feld namens "X-API-Key" suchen


api_key_header = APIKeyHeader(name="X-API-Key")


api_keys = []
def create_api_key():
    pass

def get_api_key(user_id: int):
    pass

# Prüft das Passwort. Stimmt es nicht → HTTP 401 (Unauthorized)
def verify_api_key(sent_api_key: str = Security(api_key_header)):
    if sent_api_key not in api_keys :
        raise HTTPException(status_code=401, detail="Ungültiger API-Key")
    return sent_api_key