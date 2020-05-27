import { SavePersistonResponse } from "./wireTypes";

//see session.save
export default interface SaveInfo {
    success: boolean;
    details: SavePersistonResponse[];
}