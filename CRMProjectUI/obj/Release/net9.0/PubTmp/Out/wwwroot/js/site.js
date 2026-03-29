$(document).ready(function () {
    if ($.fn.DataTable.isDataTable('#tableExport')) {
        $('#tableExport').DataTable().destroy();
    }
    $('#tableExport').DataTable({
        dom: 'Bfrtip',
        buttons: [
            'copy', 'csv', 'excel', 'pdf', 'print'
        ],
        language: {
            "decimal": ",",
            "thousands": ".",
            "info": "_TOTAL_ kayıttan _START_ - _END_ arası gösteriliyor",
            "infoEmpty": "Kayıt yok",
            "infoFiltered": "(_MAX_ kayıttan filtrelendi)",
            "lengthMenu": "_MENU_ kayıt göster",
            "loadingRecords": "Yükleniyor...",
            "processing": "İşleniyor...",
            "search": "Ara:",
            "zeroRecords": "Eşleşen kayıt bulunamadı",
            "paginate": {
                "first": "İlk",
                "last": "Son",
                "next": "Sonraki",
                "previous": "Önceki"
            },
            "buttons": {
                "copy": "Kopya",
                "csv": "CSV",
                "excel": "Excel",
                "pdf": "PDF",
                "print": "Yazdır"
            }
        }
    });
});
function toggle_sidebar_mini(isMini) {
    if (isMini) {
        $('body').addClass('sidebar-mini');
    } else {
        $('body').removeClass('sidebar-mini');
    }
}
function toggle_sticky_header(isSticky) {
    if (isSticky) {
        $(".main-navbar").addClass("sticky");
    } else {
        $(".main-navbar").removeClass("sticky");
    }
}

$(document).ready(function () {
    loadThemeSettings();
    setTimeout(function () {
        initThemeEventListeners();
    }, 100);
});

function loadThemeSettings() {
    const body = $("body");
    const layout = localStorage.getItem('layout') ||
        'light';

    body.removeClass('light dark dark-layout');
    if (layout === 'dark') {
        body.addClass('dark dark-sidebar theme-black');
        $('.select-layout[value="2"]').prop('checked', true);
        $('.select-sidebar[value="2"]').prop('checked', true);
        $('.choose-theme li').removeClass('active');
        $('.choose-theme li[title="black"]').addClass('active');
    } else {
        body.addClass('light light-sidebar theme-white');
        $('.select-layout[value="1"]').prop('checked', true);
        $('.select-sidebar[value="1"]').prop('checked', true);
        $('.choose-theme li').removeClass('active');
        $('.choose-theme li[title="white"]').addClass('active');
    }


    const themeColor = localStorage.getItem('themeColor') ||
        'white';
    const themeClasses = ['white', 'cyan', 'black', 'purple', 'orange', 'green', 'red'];
    themeClasses.forEach(theme => body.removeClass('theme-' + theme));

    body.addClass('theme-' + themeColor);
    $('.choose-theme li').removeClass('active');
    $('.choose-theme li[title="' + themeColor + '"]').addClass('active');

    const miniSidebar = localStorage.getItem('miniSidebar') === 'true';
    $('#mini_sidebar_setting').prop('checked', miniSidebar);
    if (miniSidebar && typeof toggle_sidebar_mini === 'function') {
        toggle_sidebar_mini(true);
    }
    const stickyHeader = localStorage.getItem('stickyHeader');
    if (stickyHeader === null) {
        localStorage.setItem('stickyHeader', 'true');
        $('#sticky_header_setting').prop('checked', true);
        if (typeof toggle_sticky_header === 'function') {
            toggle_sticky_header(true);
        }
    } else {
        const isSticky = stickyHeader === 'true';
        $('#sticky_header_setting').prop('checked', isSticky);
        if (typeof toggle_sticky_header === 'function') {
            toggle_sticky_header(isSticky);
        }
    }
}
function initThemeEventListeners() {

    $('.layout-color input:radio').unbind('change').on('change', function () {
        const layout = $(this).val() === '2' ? 'dark' : 'light';
        localStorage.setItem('layout', layout);

        if ($(this).val() == "1") {
            $("body").removeClass();

            $("body").addClass("light light-sidebar theme-white");
            $(".choose-theme li").removeClass("active");
            $(".choose-theme li[title='white']").addClass("active");
            $(".select-sidebar[value='1']").prop("checked", true);
            localStorage.setItem('sidebarColor', '1');

            localStorage.setItem('themeColor', 'white');
        } else {
            $("body").removeClass();
            $("body").addClass("dark dark-sidebar theme-black");
            $(".choose-theme li").removeClass("active");
            $(".choose-theme li[title='black']").addClass("active");
            $(".select-sidebar[value='2']").prop("checked", true);
            localStorage.setItem('sidebarColor', '2');
            localStorage.setItem('themeColor', 'black');
        }
    });
    $('.sidebar-color input:radio').unbind('change').on('change', function () {
        const sidebarColor = $(this).val();
        localStorage.setItem('sidebarColor', sidebarColor);

        if ($(this).val() == "1") {
            $("body").removeClass("dark-sidebar").addClass("light-sidebar");
        } else {
            $("body").removeClass("light-sidebar").addClass("dark-sidebar");
        }
    });
    $('.choose-theme li').unbind('click').on('click', function () {
        const themeColor = $(this).attr('title');
        localStorage.setItem('themeColor', themeColor);

        const bodytag = $('body');
        const prevTheme = $('.choose-theme li.active').attr('title');

        $('.choose-theme li').removeClass('active');
        $(this).addClass('active');

        if (prevTheme) {

            bodytag.removeClass('theme-' + prevTheme);
        }
        bodytag.addClass('theme-' + themeColor);
    });
    $('#mini_sidebar_setting').unbind('change').on('change', function () {
        const isChecked = $(this).is(':checked');
        localStorage.setItem('miniSidebar', isChecked);

        if (typeof toggle_sidebar_mini === 'function') {
            toggle_sidebar_mini(isChecked);
        }
    });
    $('#sticky_header_setting').unbind('change').on('change', function () {
        const isChecked = $(this).is(':checked');
        localStorage.setItem('stickyHeader', isChecked);

        if (typeof toggle_sticky_header === 'function') {
            toggle_sticky_header(isChecked);
        }
    });
    $('.btn-restore-theme').unbind('click').on('click', function (e) {
        e.preventDefault();

        localStorage.clear();

        localStorage.setItem('layout', 'light');

        localStorage.setItem('sidebarColor', '1');
        localStorage.setItem('themeColor', 'white');
        localStorage.setItem('miniSidebar', 'false');
        localStorage.setItem('stickyHeader', 'true');

        $("body").removeClass();
        $("body").addClass("light light-sidebar theme-white");

        $(".choose-theme li").removeClass("active");
        $(".choose-theme li[title='white']").addClass("active");
        $(".select-layout[value='1']").prop("checked", true);
        $(".select-sidebar[value='1']").prop("checked", true);

        if (typeof toggle_sidebar_mini === 'function') {
            toggle_sidebar_mini(false);
        }
        $("#mini_sidebar_setting").prop("checked", false);
        $("#sticky_header_setting").prop("checked", true);
        if (typeof toggle_sticky_header === 'function') {
            toggle_sticky_header(true);
        }
    });
}