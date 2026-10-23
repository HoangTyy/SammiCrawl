import { useId } from 'react';
import { CircleAlert } from 'lucide-react';
export function HelpTooltip({text,label='Hướng dẫn'}:{text:string;label?:string}) {
 const id=useId();
 return <span className="help-tooltip"><button type="button" className="help-tooltip-trigger" aria-label={label} aria-describedby={id}><CircleAlert size={18}/></button><span role="tooltip" id={id}>{text}</span></span>;
}
