from fastapi_restful.cbv import cbv
from pydantic import BaseModel

import models

from sqlalchemy.orm import Session
from fastapi.params import Depends

from database import get_db
from fastapi import APIRouter

from routers.auth import verify_api_key
from routers.base import BaseAPI

router = APIRouter(prefix="/user", tags=["User"])

# Pydentic Schemas
class UserCreate(BaseModel):
    email: str
    password: str
    storage_plan_key: str

class UserResponse(BaseModel):
    id: int
    email : str
    storage_plan: int


@cbv(router)
class UserAPI(BaseAPI):

    db: Session = Depends(get_db())

    api_key : str = Depends(verify_api_key)

    @router.post("/")
    def create_user(self,user :UserCreate):
        new_user = models.DBUser(**user.model_dump())
        self.db.add(new_user)
        self.db.commit()
        self.db.refresh(new_user)



    @router.get("/{user_id}", response_model=UserResponse)
    def get_user(self, user_id: int):
        self.get_or_404(self.db, models.DBUser, user_id)


