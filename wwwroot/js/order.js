var dataTable;
$(document).ready(function () {
    var url = window.location.search;
    if (url.includes("cash")) {
        loadDataTable("cash");
    }
    else if (url.includes("completed")) {
        loadDataTable("completed");
    }
    else if (url.includes("pending")) {
        loadDataTable("pending");
    }
    else if (url.includes("creditCart")) {
        loadDataTable("creditCart");
    }
    else {
        loadDataTable("all");
    }
});

function loadDataTable(status) {
    if ($.fn.DataTable.isDataTable('#tblData')) {
        $('#tblData').DataTable().destroy();
    }

    dataTable = $('#tblData').DataTable({
        "ajax": { url: '/order/getall?status=' + status },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "15%" },
            { data: 'phoneNumber', "width": "15%" },
            { data: 'tableNo', "width": "5%" },
            { data: 'orderStatus', "width": "10%" },
            { data: 'orderDelivered', "width": "10%" },
            { data: 'orderTotal', "width": "10%" },
            {
                data: 'id',
                "render": function (data, type, row) {
                    let buttons = `<div class="w-100 d-flex justify-content-center">
                                    <a href="/order/details?orderId=${data}" class="btn btn-primary mx-2"> <i class="bi bi-pencil-square"></i></a>`;

                    if (row.orderStatus === 'Pending') {
                        buttons += `<a href="/order/PendingToCash?id=${data}" class="btn btn-primary mx-2"> <i class="bi bi-cash-coin"></i></a>`;
                    }

                    if (row.orderStatus === 'Cash' || row.orderStatus === 'CreditCart') {
                        if (row.orderDelivered !== 'Shipped') {
                            buttons += `<a href="/order/OrderDelivered?id=${data}" class="btn btn-primary mx-2"> <i class="bi bi-check-square-fill"></i></a>`;
                        }
                        
                    }

                    buttons += `</div >`;

                    return buttons;
                },
                "width": "10%"
            }
        ],
        "createdRow": function (row, data, dataIndex) {
            if (data.orderStatus === "Cancelled") {
                $(row).addClass('bg-danger text-white');
            }
            else if (data.orderStatus === "Cash") {
                $(row).addClass('bg-success text-white');
            }
            else if (data.orderStatus === "CreditCart") {
                $(row).addClass('bg-info text-white');
            }
        }
    });
}