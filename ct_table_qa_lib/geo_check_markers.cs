using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ct_table_qa_lib
{
    public class geo_check_markers
    {
        string combine(string path1, string path2)
        {
            return System.IO.Path.Combine(path1, path2);
        }

        void delete_file(string path)
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        public void process_baseline()
        {
            string baseline_dir = global_variables.machine_param.get_value("baseline_dir");
            analize(baseline_dir, combine(baseline_dir, "out"));
        }

        double[] get_point(string file, string pt_name)
        {
            param p_pts = new param(file);
            return toDouble(p_pts.get_value_as_array(pt_name));
        }

        string pass_fail(double[] err, double tol)
        {
            foreach(double value in err)
            {
                if (Math.Abs(value) > tol)
                    return "fail";
            }

            return "pass";
        }

        string print_line(string title, double[] pt, double[] pt_ref, double tol, string num_format)
        {
            double[] err = sub(pt, pt_ref);

            return string.Format("{0},{1},{2},{3},{4},{5}",
                    title,
                    toString(pt, num_format),
                    toString(pt_ref, num_format),
                    toString(sub(pt,pt_ref), num_format),
                    norm(err).ToString(num_format),
                    pass_fail(err, tol));
        }

        public void run(string case_dir)
        {

            global_variables.log_line("run()");
            global_variables.log_line("case_dir=" + case_dir);

            //////////////////////////
            // do the analysis
            global_variables.log_line("analizing data...");
            string out_dir = combine(case_dir, "out");

            System.IO.Directory.CreateDirectory(out_dir);
            analize(case_dir, out_dir);

            string num_format = "0.0";

            ///////////////////
            // load the points 
            double[] pt0_center = get_point(combine(case_dir, "table_center//out//points.txt"), "pt0");
            double[] pt1_center = get_point(combine(case_dir, "table_center//out//points.txt"), "pt1");
            double[] pt0_low = get_point(combine(case_dir, "table_low//out//points.txt"), "pt0");
            double[] pt1_low = get_point(combine(case_dir, "table_low//out//points.txt"), "pt1");

            //save detected points 
            double[] pt0_center_ref = new double[] { 0, 0, 0 };
            double[] pt1_center_ref = new double[] { 0, 0, 840 };
            double[] pt0_low_ref = new double[] { 0, 150, 0 };
            double[] pt1_low_ref = new double[] { 0, 150, 840 };

            double[] pt0_center_err = sub(pt0_center, pt0_center_ref);
            double[] pt1_center_err = sub(pt1_center, pt1_center_ref);
            double[] pt0_low_err = sub(pt0_low, pt0_low_ref);
            double[] pt1_low_err = sub(pt1_low, pt1_low_ref);

            double point_tol = System.Convert.ToDouble(global_variables.machine_param.get_value("point_tol"));

            {
                List<string> lines = new List<string>();
                lines.Add("Points, x, y, z, x_ref, y_ref, z_ref,x_err, y_err, z_err, |err|, pass/fail");
                lines.Add(print_line("P_00", pt0_center, pt0_center_ref, point_tol, num_format));
                lines.Add(print_line("P_01", pt1_center, pt1_center_ref, point_tol, num_format));
                lines.Add(print_line("P_10", pt0_low, pt0_low_ref, point_tol, num_format));
                lines.Add(print_line("P_11", pt1_low, pt1_low_ref, point_tol, num_format));
                System.IO.File.WriteAllLines(combine(out_dir, "points.csv"), lines.ToArray());
            }

            ///////////////////////
            //save vectors 
            // LNG
            double[] v_lng1 = sub(pt1_center, pt0_center);
            double[] v_lng2 = sub(pt1_low, pt0_low);
            double[] v_lng = div(add(v_lng1, v_lng2), 2.0); // mean vector

            double[] v_lng_ref = new double[] { 0, 0, 840 };

            double[] v_lng1_err = sub(v_lng1, v_lng_ref);
            double[] v_lng2_err = sub(v_lng2, v_lng_ref);
            double[] v_lng_err = sub(v_lng, v_lng_ref);

            // VRT
            double[] v_vrt1 = sub(pt0_low, pt0_center);
            double[] v_vrt2 = sub(pt1_low, pt1_center);
            double[] v_vrt = div(add(v_vrt1, v_vrt2), 2.0); // mean vector

            double[] v_vrt_ref = new double[] { 0, 150, 0 };
            double[] v_vrt1_err = sub(v_vrt1, v_vrt_ref);
            double[] v_vrt2_err = sub(v_vrt2, v_vrt_ref);
            double[] v_vrt_err = sub(v_vrt, v_vrt_ref);

            double vector_tol = System.Convert.ToDouble(global_variables.machine_param.get_value("vector_tol"));

            {
                List<string> lines = new List<string>();
                lines.Add("Vectors, x, y, z, x_ref, y_ref, z_ref,x_err, y_err, z_err, |err|, pass/fail");
                                
                lines.Add(print_line("V_LNG1", v_lng1, v_lng_ref, vector_tol, num_format));
                lines.Add(print_line("V_LNG2", v_lng2, v_lng_ref, vector_tol, num_format));
                lines.Add(print_line("V_LNG_AVG", v_lng, v_lng_ref, vector_tol, num_format));

                lines.Add(print_line("V_VRT1", v_vrt1, v_vrt_ref, vector_tol, num_format));
                lines.Add(print_line("V_VRT2", v_vrt2, v_vrt_ref, vector_tol, num_format));
                lines.Add(print_line("V_VRT_AVG", v_vrt, v_vrt_ref, vector_tol, num_format));

                System.IO.File.WriteAllLines(combine(out_dir, "vectors.csv"), lines.ToArray());
            }

            ///////////
            // offsets
            double offset_tol = System.Convert.ToDouble(global_variables.machine_param.get_value("offset_tol"));
            {
                StringBuilder sb_offsets = new StringBuilder();

                sb_offsets.AppendLine("Offsets, x (LAT), y (VRT), z (LNG), |err|, pass/fail");

                // laser offset (BB was aligned based on CT internal lasers)
                double[] laser_offset_wrt_image_origin = pt0_center;
                sb_offsets.AppendLine(string.Format("CT Internal Laser Offset,{0},{1},{2}", 
                    toString(laser_offset_wrt_image_origin, num_format),
                    norm(laser_offset_wrt_image_origin).ToString(num_format),
                    pass_fail(laser_offset_wrt_image_origin, offset_tol)));

                // table VRT travel offset
                double[] table_travel_offset_lng = v_lng_err;
                sb_offsets.AppendLine(string.Format("Table Travel Offset(LNG),{0},{1},{2}", 
                    toString(table_travel_offset_lng, num_format),
                    norm(table_travel_offset_lng).ToString(num_format),
                    pass_fail(table_travel_offset_lng, offset_tol)));

                // table VRT travel offset
                double[] table_travel_offset_vrt = v_vrt_err;
                sb_offsets.AppendLine(string.Format("Table Travel Offset (VRT),{0},{1},{2}", 
                    toString(table_travel_offset_vrt, num_format),
                    norm(table_travel_offset_vrt).ToString(num_format),
                    pass_fail(table_travel_offset_vrt, offset_tol)));

                System.IO.File.WriteAllText(combine(out_dir, "offsets.csv"), sb_offsets.ToString());
            }

            // html report
            {
                string case_result_dir = combine(case_dir, "out");

                string dirname = System.IO.Path.GetFileName(case_dir);
                string date = dirname.Split('_')[0];
                string time = dirname.Split('_')[1];
                string user = dirname.Split('_')[2];

                // read template 
                string html_template_file = global_variables.machine_param.get_value("html_report_template");
                if (!System.IO.File.Exists(html_template_file))
                {
                    global_variables.log_error("report template not found: " + html_template_file);
                    return;
                }

                string html = System.IO.File.ReadAllText(html_template_file);

                string report_title = global_variables.machine_param.get_value("report_title");

                html = html.Replace("{{{date}}}", date)
                    .Replace("{{{time}}}", time)
                    .Replace("{{{user}}}", user)
                    .Replace("{{{title}}}", report_title);

                //double point_to_point_dist_tol = System.Convert.ToDouble(global_variables.machine_param.get_value("point_to_point_dist_tol"));
                //double dist_tol = System.Convert.ToDouble(global_variables.machine_param.get_value("dist_tol"));

                html = html
                .Replace("{{{point_tol}}}", global_variables.machine_param.get_value("point_tol"))
                .Replace("{{{vector_tol}}}", global_variables.machine_param.get_value("vector_tol"))
                .Replace("{{{offset_tol}}}", global_variables.machine_param.get_value("offset_tol"));

                html = html
                        .Replace("{{{point_rows}}}", gen_html_table_rows_from_csv(case_result_dir, "points.csv"))
                        .Replace("{{{vector_rows}}}", gen_html_table_rows_from_csv(case_result_dir, "vectors.csv"))
                        .Replace("{{{offset_rows}}}", gen_html_table_rows_from_csv(case_result_dir, "offsets.csv"));

                // save the report
                string html_file = combine(case_result_dir, "report.html");
                System.IO.File.WriteAllText(html_file, html);

            }

            ///////////////////////
            ////// email the report
            //////case_dir = @"W:\RadOnc\Planning\Physics QA\CTQA\GECTSH\cases\20190412_111928";
            //global_variables.log_line("emailing report...");
            //email_report(case_dir);

            global_variables.log_line("exiting geo_check_markers.run()...");
        }

        double[] calculate_plane_normal_from_three_points(List<double[]> points)
        {
            double[] pt0 = points[0];
            double[] pt1 = points[1];
            double[] pt2 = points[2];

            double[] v1 = sub(pt1, pt0);
            double[] v2 = sub(pt2, pt0);

            double[] norm = cross(v1, v2);

            return unit_vector(norm);
        }

        double[] unit_vector(double[] v)
        {
            double length = norm(v);
            return div(v, length);
        }


        double norm(double[] values)
        {
            return Math.Sqrt(squared_sum(values));
        }

        double angle_between_vectors_deg(double[] v1, double[] v2)
        {
            double y = norm(v1) * norm(v2) / dot(v1, v2);
            // y value cannot be larger than 1.0
            if (y > 1.0)
                return 0;

            double th = Math.Acos(y);
            return th * 180 / Math.PI;
        }

        double squared_sum(double[] values)
        {
            double sum = 0.0;
            foreach (double v in values)
                sum += v * v;
            return sum;
        }


        double[] calculate_plane_normal_from_points(List<double[]> points)
        {
            List<double[]> vectors = new List<double[]>();
            for (int i = 0; i < points.Count(); i++)
            {
                for (int j = 0; j < points.Count(); j++)
                {
                    if (j == i)
                        continue;

                    double[] v = sub(points[j], points[i]);

                    vectors.Add(v);
                }
            }
            return calculate_plane_normal_from_vectors(vectors);
        }

        double[] calculate_plane_normal_from_vectors(List<double[]> vectors)
        {
            List<double[]> cross_vectors = new List<double[]>();
            for (int i = 0; i < vectors.Count(); i++)
            {
                for (int j = 0; j < vectors.Count(); j++)
                {
                    if (j == i)
                        continue;

                    double[] cross_v = cross(vectors[j], vectors[i]);

                    cross_vectors.Add(cross_v);
                }
            }

            return mean_vector(cross_vectors);
        }

        double[] mean_vector(List<double[]> vectors)
        {
            double[] mean = new double[3];
            for (int c = 0; c < 3; c++)
            {
                double sum = 0.0;
                for (int v = 0; v < vectors.Count; v++)
                {
                    sum += vectors[v][c];
                }

                mean[c] = sum / vectors.Count;
            }

            return mean;
        }

        public void email_report(string case_dir)
        {
            string html_file = System.IO.Path.Combine(case_dir, @"out\report.html");

            param p = global_variables.machine_param;
            string from = p.get_value("email_from");
            string to = p.get_value("email_to");
            string from_enc_pw = p.get_value("email_from_enc_pw");
            string body = System.IO.File.ReadAllText(html_file);
            string domain = p.get_value("email_domain");
            string host = p.get_value("email_host_address");
            int port = System.Convert.ToInt32(p.get_value("email_host_port"));
            bool enable_ssl = System.Convert.ToBoolean(p.get_value("enable_ssl"));
            //send
            email.send(from, from_enc_pw, to, "CTQA (" + p.get_value("machine") + ")", body, domain, host, port, enable_ssl);
        }

        string gen_html_table_rows_from_csv(string case_result_dir, string filename, string num_format = "0.0")
        {
            string file = combine(case_result_dir, filename);

            // add rows
            StringBuilder sb = new StringBuilder();

            string[] lines = System.IO.File.ReadAllLines(file);

            // header line
            {
                sb.AppendLine("<tr>");
                string line = lines[0];
                foreach (string value in line.Split(','))
                    sb.AppendLine("<th>" + value + "</th>");
                sb.AppendLine("</tr>");
            }

            // rows
            for (int i = 1; i < lines.Length; i++)
            {

                string line = lines[i];

                if (line.ToLower().Contains("fail"))
                    sb.AppendLine("<tr class='fail'>");
                else
                    sb.AppendLine("<tr>");

                foreach (string value in line.Split(','))
                    sb.AppendLine("<td>" + value + "</td>");
                sb.AppendLine("</tr>");
            }
            return sb.ToString();
        }



        void analize(string case_dir, string out_dir)
        {
            // table center
            string center_dir = global_variables.combine(case_dir, "table_center");
            analize_CT(center_dir, 0.0);

            // table low
            string low_dir = global_variables.combine(case_dir, "table_low");
            analize_CT(low_dir, 150); // table moved down by 150 mm.
        }

        void analize_CT(string img_dir, double table_shift_y)
        {
            global_variables.log_line("analizing CT:" + img_dir);

            string out_dir = global_variables.combine(img_dir, "out");
            if (!System.IO.Directory.Exists(out_dir))
                System.IO.Directory.CreateDirectory(out_dir);

            string img = combine(out_dir, "CT.mhd");
            if (!System.IO.File.Exists(img))
            {
                // case CT not found, try to convert dicom files to CT.mhd
                global_variables.log_line("CT.mhd not found:" + img);
                global_variables.log_line("Trying to convert dicom images to CT.mhd...");
                dicomtools.dicom_series_to_mhd(img_dir, out_dir);

                if (!System.IO.File.Exists(img))
                {
                    global_variables.log_error("Count not create CT.mhd! Please check you have dicom CT files or CT.mhd in the case folder:" + out_dir);
                }
            }

            // threshold
            string img_th = img + ".th.mhd";
            string img_tmp = combine(out_dir, "tmp.mha");
            if (!System.IO.File.Exists(img_th))
            {
                double th = System.Convert.ToDouble(global_variables.machine_param.get_value("th"));

                delete_file(img_tmp);

                imagetools.threshold_3d_f(img, 0, th, 255, img_tmp);
                imagetools.cast_to_uchar_3d_f(img_tmp, img_th);

                delete_file(img_tmp);

                if (!System.IO.File.Exists(img_th))
                {
                    global_variables.log_error("Image threshold failed! File not found." + img_th);
                }
            }

            // Find the points
            int num_of_points = System.Convert.ToInt32(global_variables.machine_param.get_value("num_of_markers"));
            double search_box_size_mm = System.Convert.ToDouble(global_variables.machine_param.get_value("search_box_size_mm"));

            param p_img = new param(img);

            List<double[]> points = new List<double[]>();
            for (int i = 0; i < num_of_points; i++)
            {
                string point_name = "pt" + i.ToString();
                double[] pt = toDouble(global_variables.machine_param.get_value_as_array(point_name));

                // add table shift
                pt[1] += table_shift_y;

                // ROI
                double[] pt_low = add(pt, -search_box_size_mm / 2.0);
                double[] pt_high = add(pt, search_box_size_mm / 2.0);

                // ROI in image coordinate system
                double[] spacing = toDouble(p_img.get_value("ElementSpacing").Split());
                double[] org = toDouble(p_img.get_value("Offset").Split());

                // ROI in image index coord
                int[] pt_low_I = toInt(add(div(sub(pt_low, org), spacing), 0.49999));
                int[] pt_high_I = toInt(add(div(sub(pt_high, org), spacing), 0.49999));

                // ROI size 
                int[] size_I = sub(pt_high_I, pt_low_I);

                string moment_file = combine(out_dir, point_name + ".moments.txt");
                {
                    imagetools.calc_image_moments_3d_f(img_th, pt_low_I[0], pt_low_I[1], pt_low_I[2],
                         size_I[0], size_I[1], size_I[2], moment_file);

                    // read the com
                    param p = new param(moment_file);
                    string com_string = p.get_value("Center of gravity").Replace('[', ' ').Replace(']', ' ');
                    double[] com = toDouble(com_string.Split(','));

                    points.Add(com);
                }
            }

            // save the points to a file
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < num_of_points; i++)
            {
                string point_name = "pt" + i.ToString();
                double[] pt = points[i];
                sb.AppendLine(string.Format("{0}={1},{2},{3}", point_name, pt[0], pt[1], pt[2]));
            }

            string points_file = combine(out_dir, "points.txt");
            System.IO.File.WriteAllText(points_file, sb.ToString());

            //////////////
            // Distances
            StringBuilder sb_dist = new StringBuilder();
            for (int r = 0; r < num_of_points; r++)
                for (int c = r + 1; c < num_of_points; c++)
                {
                    double[] pt_row = points[r];
                    double[] pt_col = points[c];

                    double d = dist(pt_row, pt_col);

                    string line = string.Format("dist_pt{0}_pt{1}={2}", r, c, d);

                    sb_dist.AppendLine(line);
                }

            // save the distance
            string dist_file = combine(out_dir, "distances.txt");
            System.IO.File.WriteAllText(dist_file, sb_dist.ToString());
        }

        string toString(double[] values, string num_format)
        {
            List<string> list = new List<string>();
            foreach (double v in values)
                list.Add(string.Format("{0:" + num_format + "}", v));

            return string.Join(",", list);
        }

        double dist(double[] pt1, double[] pt2)
        {
            double[] d = sub(pt1, pt2);
            return Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
        }

        double[] cross(double[] a, double[] b)
        {
            double[] c = new double[3];

            c[0] = a[1] * b[2] - a[2] * b[1];
            c[1] = a[2] * b[0] - a[0] * b[2];
            c[2] = a[0] * b[1] - a[1] * b[0];

            return c;
        }

        double dot(double[] a, double[] b)
        {
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];

            return sum;
        }


        double[] toDouble(string[] values)
        {
            List<double> list = new List<double>();
            foreach (string s in values)
                list.Add(System.Convert.ToDouble(s));

            return list.ToArray();
        }

        double min(double[] values)
        {
            double min = double.MaxValue;
            foreach (double v in values)
            {
                if (v < min)
                    min = v;
            }
            return min;
        }

        double max(double[] values)
        {
            double max = double.MinValue;
            foreach (double v in values)
            {
                if (v > max)
                    max = v;
            }
            return max;
        }

        double mean(double[] values)
        {
            double sum = 0.0;
            foreach (double v in values)
            {
                sum += v;
            }
            return (sum / values.Length);
        }

        double std(double[] values)
        {
            double m = mean(values);

            double sum = 0.0;
            foreach (double v in values)
            {
                sum += (v - m) * (v - m);
            }

            return Math.Sqrt(sum / values.Length);
        }

        int[] toInt(double[] values)
        {
            List<int> list = new List<int>();
            foreach (double v in values)
                list.Add(System.Convert.ToInt32(v));

            return list.ToArray();
        }


        string[] toString(double[] values)
        {
            List<string> list = new List<string>();
            foreach (double s in values)
                list.Add(s.ToString());

            return list.ToArray();
        }

        double[] div(double[] values, double denom)
        {
            List<double> list = new List<double>();
            foreach (double v in values)
                list.Add(v / denom);

            return list.ToArray();
        }

        double[] add(double[] values, double a)
        {
            List<double> list = new List<double>();
            foreach (double v in values)
                list.Add(v + a);

            return list.ToArray();
        }

        double[] sub(double[] values, double a)
        {
            List<double> list = new List<double>();
            foreach (double v in values)
                list.Add(v - a);

            return list.ToArray();
        }


        double[] add(double[] values1, double[] values2)
        {
            List<double> list = new List<double>();
            for (int i = 0; i < values1.Length; i++)
                list.Add(values1[i] + values2[i]);

            return list.ToArray();
        }

        double[] sub(double[] values1, double[] values2)
        {
            List<double> list = new List<double>();
            for (int i = 0; i < values1.Length; i++)
                list.Add(values1[i] - values2[i]);

            return list.ToArray();
        }

        double[] div(double[] values1, double[] values2)
        {
            List<double> list = new List<double>();
            for (int i = 0; i < values1.Length; i++)
                list.Add(values1[i] / values2[i]);

            return list.ToArray();
        }

        int[] sub(int[] values1, int[] values2)
        {
            List<int> list = new List<int>();
            for (int i = 0; i < values1.Length; i++)
                list.Add(values1[i] - values2[i]);

            return list.ToArray();
        }




    }
}
