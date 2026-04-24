import { getData} from "./data"
import { Filemanager } from "@svar-ui/react-filemanager";
import "@svar-ui/react-filemanager/all.css";
import { Willow } from "@svar-ui/react-filemanager";
import '@svar-ui/react-core/style.css';
import '@svar-ui/react-menu/style.css';
import '@svar-ui/react-grid/style.css';
import '@svar-ui/react-uploader/style.css';

//TODO connect to backend and implement the file manager, this is just a placeholder for now
function BaseManager() {
  return (
    <div className="fm-container">
      <Willow>
        <Filemanager data={getData()} />
      </Willow>
      </div>
  )
}

export default BaseManager;