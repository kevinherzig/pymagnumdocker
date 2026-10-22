export interface Summary {
  vdc: number | null; soc: number | null; battery_amps: number | null; charge_amps: number | null;
  battery_temp: number | null; ac_out_w: number | null; ac_in_w: number | null;
  vac_in: number | null; vac_out: number | null; hz: number | null; shore: boolean;
  mode_text: string | null; fault: number; fault_text: string | null;
  invled_text: string | null; chgled_text: string | null;
  ags_status: string | null; ags_running: boolean | null;
  pv_volts: number | null; pt100_mode: string | null;
}
export interface State { updated: number | null; age: number | null; error: string | null; summary: Summary | null; seq: number; }
export interface History { fields: string[]; rows: (number | null)[][]; }
