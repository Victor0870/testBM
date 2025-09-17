using UnityEngine;
using TMPro;
using daBizmate;


public class Testdatabase : MonoBehaviour
{

    public TextMeshProUGUI text1;
     void Start()
        {
            // Đảm bảo myTextOutput đã được gán
            if (text1 == null)
            {
                Debug.LogError("My Text Output (TMP_Text) chưa được gán trong Inspector!");
                return;
            }

            DisplayTest4Data();
        }

        private void DisplayTest4Data()
        {
            // Kiểm tra xem bảng E_test4 có tồn tại và có ít nhất 2 dòng không
            if (E_test4.CountEntities < 2)
            {
                Debug.LogWarning("Bảng test4 không có đủ dòng dữ liệu (cần ít nhất 2 dòng).");
                text1.text = "Dữ liệu không đủ.";
                return;
            }

            // Lấy thực thể (entity) ở dòng thứ 2 (index 1)
            E_test4 secondRowEntity = E_test4.GetEntity(1);

            if (secondRowEntity != null)
            {
                // Lấy giá trị của trường f_test3
                string test3Value = E_test4._f_test3[1];

                int rowCount = E_test1.CountEntities;


                // Gán giá trị vào đối tượng TMP_Text
                text1.text = $"Giá trị là: {test3Value}";
                Debug.Log($"Đã lấy giá trị: {test3Value}");
            }
            else
            {
                Debug.LogError("Không tìm thấy thực thể ở dòng thứ 2 của bảng test4.");
                text1.text = "Lỗi: Không thể lấy dữ liệu.";
            }
        }
}
